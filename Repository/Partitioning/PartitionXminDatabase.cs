using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using Npgsql;
using NpgsqlTypes;
using System.Transactions;

namespace Repository.Partitioning;

/// <summary>
/// 通过公共存储扩展在 EF 保存通知之前批量回读分区新增行的真实 xmin
/// </summary>
public sealed class PartitionXminDatabase(DatabaseDependencies dependencies, RelationalDatabaseDependencies relationalDependencies, ICurrentDbContext currentContext) : RelationalDatabase(dependencies, relationalDependencies)
{

    // 仅保存当前上下文模型与启动快照的不变映射，不缓存条目、主键值、版本或事务状态
    private readonly Dictionary<IEntityType, ReadbackMapping?> readbackMappings = new();


    /// <summary>
    /// 使用当前上下文的连接、事务和公共查询入口
    /// </summary>
    private DatabaseFacade Database => currentContext.Context.Database;


    /// <summary>
    /// 在同步存储阶段保存实体并暂存真实版本
    /// </summary>
    public override int SaveChanges(IList<IUpdateEntry> entries)
    {

        return SavePartitionChangesAsync(entries, false, CancellationToken.None).GetAwaiter().GetResult();

    }


    /// <summary>
    /// 在异步存储阶段保存实体并暂存真实版本
    /// </summary>
    public override Task<int> SaveChangesAsync(IList<IUpdateEntry> entries, CancellationToken cancellationToken = default)
    {

        return SavePartitionChangesAsync(entries, true, cancellationToken);

    }


    /// <summary>
    /// 在存储阶段完成写入和回读，生成值的接受与失败清理由 EF 负责
    /// </summary>
    private async Task<int> SavePartitionChangesAsync(IList<IUpdateEntry> entries, bool asynchronous, CancellationToken cancellationToken)
    {

        var updateAdapter = Dependencies.UpdateAdapterFactory.Create();
        // 无快照只允许离线建模和查询，不能把未初始化误当作没有分区
        _ = Snapshot;
        var insertedPartitions = entries.Where(x => RequiresXminReadback(x, updateAdapter)).ToArray();
        if (insertedPartitions.Length == 0)
        {
            return asynchronous ? await base.SaveChangesAsync(entries, cancellationToken).ConfigureAwait(false) : base.SaveChanges(entries);
        }

        cancellationToken.ThrowIfCancellationRequested();
        foreach (var entry in insertedPartitions)
        {
            var keys = GetReadbackMapping(entry.EntityType)!.Keys;
            if (keys.Any(p => entry.HasTemporaryValue(p) || entry.GetCurrentValue(p) == null || p.ValueGenerated != ValueGenerated.Never))
            {
                throw new NotSupportedException($"分区实体 {entry.EntityType.Name} 必须在插入前确定全部主键值，不能使用数据库生成的主键");
            }
        }

        var connection = currentContext.Context.GetService<IRelationalConnection>();
        var manager = (ITransactionEnlistmentManager)connection;
        // 使用提供程序的登记策略，尊重连接明确配置的 Enlist=false
        var ambient = manager.EnlistedTransaction ?? manager.CurrentAmbientTransaction;
        if (ambient != null)
        {
            if (Database.CurrentTransaction != null)
            {
                throw new InvalidOperationException("分区保存不能同时使用本地事务和环境事务");
            }
            if (ambient.TransactionInformation.Status != TransactionStatus.Active)
            {
                throw new InvalidOperationException("分区保存要求环境事务处于活动状态");
            }
            return await SaveAmbientChangesAsync(entries, insertedPartitions, connection, asynchronous, cancellationToken).ConfigureAwait(false);
        }

        var transaction = Database.CurrentTransaction;
        // 插入和回读必须共用事务，避免回填到其他连接更新后的版本而漏报并发冲突
        if (transaction == null && Database.AutoTransactionBehavior == AutoTransactionBehavior.Never)
        {
            throw new InvalidOperationException("新增分区记录需要事务保护写入和 xmin 回读：当前 AutoTransactionBehavior 为 Never，且没有可用的外层事务。请恢复 WhenNeeded/Always，或通过 BeginTransaction、UseTransaction 或有效登记的 TransactionScope 提供事务后再保存");
        }

        // 已有事务但显式关闭自动保存点时，由调用方承担失败后的整体回滚
        if (transaction != null && !Database.AutoSavepointsEnabled)
        {
            return await SaveAndReadVersionsAsync(entries, insertedPartitions, asynchronous, cancellationToken).ConfigureAwait(false);
        }

        var ownsTransaction = transaction == null;
        var savepoint = "partition_xmin_" + Guid.NewGuid().ToString("N");
        if (transaction != null && !transaction.SupportsSavepoints)
        {
            throw new NotSupportedException("分区 xmin 回读要求当前事务支持保存点");
        }

        if (ownsTransaction)
        {
            transaction = asynchronous ? await Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false) : Database.BeginTransaction();
        }
        else if (asynchronous)
        {
            await transaction!.CreateSavepointAsync(savepoint, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            transaction!.CreateSavepoint(savepoint);
        }

        int affected;
        try
        {
            affected = await SaveAndReadVersionsAsync(entries, insertedPartitions, asynchronous, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            if (ownsTransaction)
            {
                if (asynchronous) await transaction!.CommitAsync(cancellationToken).ConfigureAwait(false);
                else transaction!.Commit();
            }
            else
            {
                if (asynchronous) await transaction!.ReleaseSavepointAsync(savepoint, cancellationToken).ConfigureAwait(false);
                else transaction!.ReleaseSavepoint(savepoint);
            }
        }
        catch (Exception failure)
        {
            List<Exception> failures = [failure];
            try
            {
                // 已取消的保存仍需完成回滚，不能沿用取消令牌
                if (ownsTransaction)
                {
                    if (asynchronous) await transaction!.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                    else transaction!.Rollback();
                }
                else if (asynchronous)
                {
                    await transaction!.RollbackToSavepointAsync(savepoint, CancellationToken.None).ConfigureAwait(false);
                    await transaction.ReleaseSavepointAsync(savepoint, CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    transaction!.RollbackToSavepoint(savepoint);
                    transaction.ReleaseSavepoint(savepoint);
                }
            }
            catch (Exception rollbackFailure)
            {
                failures.Add(rollbackFailure);
            }

            if (failures.Count > 1)
            {
                throw new AggregateException("保存失败且事务回滚失败，请停止使用当前上下文", failures);
            }
            throw;
        }
        finally
        {
            if (ownsTransaction)
            {
                if (asynchronous) await transaction!.DisposeAsync().ConfigureAwait(false);
                else transaction!.Dispose();
            }
        }

        return affected;

    }


    /// <summary>
    /// 在环境事务中保持同一连接，并按自动保存点设置保护本次保存和回读
    /// </summary>
    private async Task<int> SaveAmbientChangesAsync(IList<IUpdateEntry> entries, IUpdateEntry[] insertedPartitions, IRelationalConnection connection, bool asynchronous, CancellationToken cancellationToken)
    {

        var helper = currentContext.Context.GetService<ISqlGenerationHelper>();
        // 增加 EF 连接引用，防止写入与批量查询之间关闭或切换物理连接
        if (asynchronous) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        else connection.Open();
        var savepoint = "ambient_xmin_" + Guid.NewGuid().ToString("N");
        var created = false;
        try
        {
            if (Database.AutoSavepointsEnabled)
            {
                await ExecuteControlAsync(helper.GenerateCreateSavepointStatement(savepoint), asynchronous, cancellationToken).ConfigureAwait(false);
                created = true;
            }
            var affected = await SaveAndReadVersionsAsync(entries, insertedPartitions, asynchronous, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (created)
            {
                await ExecuteControlAsync(helper.GenerateReleaseSavepointStatement(savepoint), asynchronous, cancellationToken).ConfigureAwait(false);
            }
            return affected;
        }
        catch (Exception failure)
        {
            if (created)
            {
                try
                {
                    // 只撤销本次保存，外层事务的完成和整体回滚仍归调用方负责
                    await ExecuteControlAsync(helper.GenerateRollbackToSavepointStatement(savepoint), asynchronous, CancellationToken.None).ConfigureAwait(false);
                    await ExecuteControlAsync(helper.GenerateReleaseSavepointStatement(savepoint), asynchronous, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception rollbackFailure)
                {
                    throw new AggregateException("环境事务局部回滚失败，请停止使用当前事务和上下文", failure, rollbackFailure);
                }
            }
            throw;
        }
        finally
        {
            if (asynchronous) await connection.CloseAsync().ConfigureAwait(false);
            else connection.Close();
        }

    }


    /// <summary>
    /// 执行由公共 SQL 生成器生成的保存点命令，不改写 EF 业务命令
    /// </summary>
    private async Task ExecuteControlAsync(string sql, bool asynchronous, CancellationToken cancellationToken)
    {

        if (asynchronous) await Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);
        else Database.ExecuteSqlRaw(sql);

    }


    /// <summary>
    /// 各事务模式共用原生写入和批量回读，避免版本处理出现分支差异
    /// </summary>
    private async Task<int> SaveAndReadVersionsAsync(IList<IUpdateEntry> entries, IUpdateEntry[] insertedPartitions, bool asynchronous, CancellationToken cancellationToken)
    {

        var affected = asynchronous ? await base.SaveChangesAsync(entries, cancellationToken).ConfigureAwait(false) : base.SaveChanges(entries);
        // 按物理表和完整主键合并，同表拆分的多个实体共用一次行版本回读
        foreach (var group in insertedPartitions.GroupBy(x => GetReadbackMapping(x.EntityType)!.TableIdentifier))
        {
            var mapping = GetReadbackMapping(group.First().EntityType)!;
            var rows = group.GroupBy(GetProviderKey, mapping.KeyComparer).Select(row => row.ToArray());
            // 限制单次回读结果及复合主键参数数量，不改变 EF 的写入批大小
            var batchSize = Math.Min(1000, 60000 / mapping.Keys.Length);
            var batchNumber = 0;
            foreach (var batch in rows.Chunk(batchSize))
            {
                batchNumber++;
                var query = CreateXminQuery(batch, out var expected);
                var versions = asynchronous ? await query.ToListAsync(cancellationToken).ConfigureAwait(false) : query.ToList();
                var seen = new HashSet<long>();
                var duplicates = 0;
                var unknown = 0;
                var invalid = 0;
                foreach (var version in versions)
                {
                    if (!seen.Add(version.Key)) duplicates++;
                    if (!expected.ContainsKey(version.Key)) unknown++;
                    if (version.Version <= 0 || version.Version > uint.MaxValue) invalid++;
                }
                var missing = expected.Keys.Count(key => !seen.Contains(key));
                if (missing != 0 || duplicates != 0 || unknown != 0 || invalid != 0)
                {
                    // 只报告映射和数量，不在异常中输出业务主键或查询参数
                    throw new InvalidOperationException($"分区 xmin 批量回读结果校验失败：表={mapping.TableSql}，批次={batchNumber}，预期行数={expected.Count}，实际行数={versions.Count}，缺失键数={missing}，重复行数={duplicates}，未知键行数={unknown}，无效版本行数={invalid}，本次保存必须回滚");
                }
                foreach (var version in versions)
                {
                    // 只回填新增条目，不刷新已有实体参与冲突检查的原始版本
                    foreach (var entry in expected[version.Key])
                    {
                        entry.SetStoreGeneratedValue(GetReadbackMapping(entry.EntityType)!.Xmin, checked((uint)version.Version), setModified: false);
                    }
                }
            }
        }

        return affected;

    }


    /// <summary>
    /// 按实体映射批量查询版本，普通雪花主键使用数组，复合主键使用参数化键表关联
    /// </summary>
    private IQueryable<PartitionVersion> CreateXminQuery(IUpdateEntry[][] rowsToRead, out Dictionary<long, IUpdateEntry[]> expected)
    {

        var mapping = GetReadbackMapping(rowsToRead[0][0].EntityType)!;
        var sqlHelper = currentContext.Context.GetService<ISqlGenerationHelper>();
        var parameters = new List<object>();
        string sql;
        if (mapping.Keys.Length == 1 && mapping.Keys[0].ClrType == typeof(long) && mapping.KeyMappings[0].Converter == null)
        {
            expected = rowsToRead.ToDictionary(x => (long)x[0].GetCurrentValue(GetReadbackMapping(x[0].EntityType)!.Keys[0])!);
            parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = expected.Keys.ToArray() });
            sql = $"SELECT {mapping.KeyColumns[0]} AS \"Key\", xmin::text::bigint AS \"Version\" FROM {mapping.TableSql} WHERE {mapping.KeyColumns[0]} = ANY (@ids)";
        }
        else
        {
            // 返回输入键的序号以支持复合主键和自定义值转换，不假定 Id 单独唯一
            expected = rowsToRead.Select((row, index) => (row, index)).ToDictionary(x => (long)x.index, x => x.row);
            var rows = new List<string>();
            using var command = Database.GetDbConnection().CreateCommand();
            foreach (var item in expected)
            {
                var values = new List<string> { item.Key.ToString(System.Globalization.CultureInfo.InvariantCulture) };
                var entry = item.Value[0];
                var entryMapping = GetReadbackMapping(entry.EntityType)!;
                for (var index = 0; index < entryMapping.Keys.Length; index++)
                {
                    var name = "key" + parameters.Count;
                    parameters.Add(entryMapping.KeyMappings[index].CreateParameter(command, name, entry.GetCurrentValue(entryMapping.Keys[index])));
                    values.Add(sqlHelper.GenerateParameterNamePlaceholder(name));
                }
                rows.Add($"({string.Join(", ", values)})");
            }
            var columns = mapping.Keys.Select((_, i) => sqlHelper.DelimitIdentifier("k" + i)).ToArray();
            var predicates = mapping.KeyColumns.Select((column, i) => $"r.{column} = keys.{columns[i]}");
            sql = $"SELECT keys.\"Ordinal\"::bigint AS \"Key\", r.xmin::text::bigint AS \"Version\" FROM {mapping.TableSql} AS r JOIN (VALUES {string.Join(", ", rows)}) AS keys(\"Ordinal\", {string.Join(", ", columns)}) ON {string.Join(" AND ", predicates)}";
        }

        // 将真实 xid 无损转换为 bigint，避免标量 uint 的默认映射与 xid 不一致
        return Database.SqlQueryRaw<PartitionVersion>(sql, parameters.ToArray()).TagWith("Partition xmin readback");

    }


    /// <summary>
    /// 按实际列名识别版本，支持可空类型、不同属性名和影子属性
    /// </summary>
    private static IProperty? FindXminProperty(IEntityType entityType)
    {

        if (entityType.GetTableName() is not { } name || entityType.IsMappedToJson()) return null;
        var table = StoreObjectIdentifier.Table(name, entityType.GetSchema());
        return entityType.GetProperties().FirstOrDefault(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType) == typeof(uint) && p.IsConcurrencyToken && p.GetColumnName(table) == "xmin");

    }


    /// <summary>
    /// 区分真实插入与同主键替换或共享从实体补建，只为真实新增行批量回读
    /// </summary>
    private bool RequiresXminReadback(IUpdateEntry entry, IUpdateAdapter updateAdapter)
    {

        if (entry.EntityState != EntityState.Added || GetReadbackMapping(entry.EntityType) is not { } mapping) return false;
        var table = mapping.Table;
        var mainEntry = entry;
        while (true)
        {
            var foreignKeys = table.GetRowInternalForeignKeys(mainEntry.EntityType).ToArray();
            var principal = foreignKeys
                .Select(foreignKey => updateAdapter.FindPrincipal(mainEntry, foreignKey)).FirstOrDefault(candidate => candidate != null);
            // 只有从实体时 EF 创建的是 UPDATE，不能把缺少跟踪主实体解释为新行
            if (principal == null)
            {
                if (foreignKeys.Length != 0) return false;
                break;
            }
            mainEntry = principal;
        }
        // 同主键旧实例仍在跟踪时，EF 将删除和新增合并为同一物理行的更新
        return mainEntry.EntityState == EntityState.Added
            && (mainEntry.SharedIdentityEntry is not { } previous
                || previous.EntityType != mainEntry.EntityType && !previous.EntityType.GetTableMappings().Any(mapping => mapping.Table == table));

    }


    /// <summary>
    /// 每个实体类型按当前上下文的启动快照解析一次映射，不读取分区维护声明
    /// </summary>
    private ReadbackMapping? GetReadbackMapping(IEntityType entityType)
    {

        if (readbackMappings.TryGetValue(entityType, out var mapping)) return mapping;
        var xmin = FindXminProperty(entityType);
        var tables = entityType.GetTableMappings().Select(item => item.Table).ToArray();
        var table = xmin == null ? null : tables.FirstOrDefault(item => Snapshot.Contains(item.Name, item.Schema));
        if (table != null)
        {
            PartitionModelBuilder.ValidateInheritance(entityType.GetRootType());
            if (tables.Length != 1 || entityType.GetMappingFragments(StoreObjectType.Table).Any())
            {
                throw new NotSupportedException($"分区实体 {entityType.Name} 不支持 Entity Splitting（SplitToTable）映射");
            }
        }
        mapping = table == null ? null : new ReadbackMapping(entityType, table, xmin!, currentContext.Context.GetService<ISqlGenerationHelper>());
        readbackMappings.Add(entityType, mapping);
        return mapping;

    }


    /// <summary>
    /// 保存必须使用宿主启动时明确加载的快照，不能回退为模型声明或空结果
    /// </summary>
    private PartitionTableSnapshot Snapshot => currentContext.Context.GetService<IDbContextOptions>()
        .FindExtension<PartitionXminOptionsExtension>()?.Snapshot
        ?? throw new InvalidOperationException("保存数据前必须在启动时加载 PartitionTableSnapshot，并传入 UsePostgreSqlPartitioning(snapshot)");


    /// <summary>
    /// 按缓存的列顺序读取当前主键并转换为数据库值，状态和参数仍逐次生成
    /// </summary>
    private object?[] GetProviderKey(IUpdateEntry entry)
    {

        var mapping = GetReadbackMapping(entry.EntityType)!;
        var values = new object?[mapping.Keys.Length];
        for (var index = 0; index < values.Length; index++)
        {
            var value = entry.GetCurrentValue(mapping.Keys[index]);
            values[index] = mapping.KeyMappings[index].Converter is { } converter ? converter.ConvertToProvider(value) : value;
        }
        return values;

    }


    /// <summary>
    /// 当前模型中单个实体类型的不可变回读映射，按实体区分共享表的属性和转换器
    /// </summary>
    private sealed class ReadbackMapping
    {

        /// <summary>
        /// 解析主键、版本和 SQL 标识符供后续记录及保存复用
        /// </summary>
        public ReadbackMapping(IEntityType entityType, ITable table, IProperty xmin, ISqlGenerationHelper sqlHelper)
        {

            Table = table;
            TableIdentifier = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());
            Xmin = xmin;
            Keys = entityType.FindPrimaryKey()!.Properties.OrderBy(p => p.GetColumnName(TableIdentifier), StringComparer.Ordinal).ToArray();
            KeyMappings = Keys.Select(p => p.GetRelationalTypeMapping()).ToArray();
            KeyComparer = new PartitionKeyComparer(Keys);
            TableSql = sqlHelper.DelimitIdentifier(TableIdentifier.Name, TableIdentifier.Schema);
            KeyColumns = Keys.Select(p => sqlHelper.DelimitIdentifier(p.GetColumnName(TableIdentifier)!)).ToArray();

        }


        /// <summary>
        /// 分区物理表及共享行关系
        /// </summary>
        public ITable Table { get; }


        /// <summary>
        /// 合并同表批次时使用的映射标识
        /// </summary>
        public StoreObjectIdentifier TableIdentifier { get; }


        /// <summary>
        /// 当前实体自己的 xmin 属性
        /// </summary>
        public IProperty Xmin { get; }


        /// <summary>
        /// 按实际列名排序的主键属性
        /// </summary>
        public IProperty[] Keys { get; }


        /// <summary>
        /// 与主键顺序一致的关系类型映射及转换器
        /// </summary>
        public RelationalTypeMapping[] KeyMappings { get; }


        /// <summary>
        /// 用于物理行去重的数据库值比较器
        /// </summary>
        public PartitionKeyComparer KeyComparer { get; }


        /// <summary>
        /// 按提供程序规则引用的表名及 schema
        /// </summary>
        public string TableSql { get; }


        /// <summary>
        /// 与主键顺序一致且已引用的列名
        /// </summary>
        public string[] KeyColumns { get; }

    }


    /// <summary>
    /// 使用提供程序值比较完整主键，避免同一物理行因实体或值转换不同而重复回读
    /// </summary>
    private sealed class PartitionKeyComparer(IProperty[] keys) : IEqualityComparer<object?[]>
    {

        private readonly ValueComparer[] comparers = keys.Select(p => p.GetProviderValueComparer()).ToArray();


        /// <summary>
        /// 比较全部主键列的数据库值
        /// </summary>
        public bool Equals(object?[]? left, object?[]? right)
        {

            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Length != right.Length) return false;
            for (var index = 0; index < left.Length; index++)
            {
                if (!comparers[index].Equals(left[index], right[index])) return false;
            }
            return true;

        }


        /// <summary>
        /// 按相同数据库值比较规则计算主键哈希
        /// </summary>
        public int GetHashCode(object?[] values)
        {

            var hash = new HashCode();
            for (var index = 0; index < values.Length; index++) hash.Add(comparers[index].GetHashCode(values[index]));
            return hash.ToHashCode();

        }

    }


    /// <summary>
    /// 公共 SQL 查询的版本结果，不加入业务实体模型
    /// </summary>
    private sealed class PartitionVersion
    {

        /// <summary>
        /// 单列主键或复合主键输入序号
        /// </summary>
        public long Key { get; set; }


        /// <summary>
        /// 数据库行真实 xmin 的无损数值
        /// </summary>
        public long Version { get; set; }

    }


}
