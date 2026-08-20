using DistributedLock;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SourceGenerator.Runtime.Attributes;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Repository.Partitioning;

/// <summary>
/// 检查 PostgreSQL 分区父表并按实体当前策略创建必要的后续子分区
/// </summary>
[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public sealed class PartitionMaintenanceService(DatabaseContext db, IDistributedLock distributedLock, ILogger<PartitionMaintenanceService> logger)
{

    /// <summary>
    /// 多实例分区维护使用的分布式锁名称
    /// </summary>
    private const string LockName = "PostgreSql.PartitionMaintenance";


    /// <summary>
    /// 当前分区之后需要保持的连续子分区数量
    /// </summary>
    private const int FuturePartitionCount = 3;


    /// <summary>
    /// 单张父表单次维护最多创建的子分区数量
    /// </summary>
    private const int MaxCreateCountPerTable = 32;


    /// <summary>
    /// 分区维护分布式锁的单次租约时长
    /// </summary>
    private static readonly TimeSpan LockLease = TimeSpan.FromMinutes(10);


    /// <summary>
    /// 分区维护分布式锁的续期间隔
    /// </summary>
    private static readonly TimeSpan LockRenewalInterval = TimeSpan.FromMinutes(3);


    /// <summary>
    /// PostgreSQL 分区边界表达式解析规则
    /// </summary>
    private static readonly Regex PartitionBoundRegex = new(
        "^FOR\\s+VALUES\\s+FROM\\s*\\(\\s*'?(-?\\d+)'?(?:::[^)]*)?\\s*\\)\\s+TO\\s*\\(\\s*'?(-?\\d+)'?(?:::[^)]*)?\\s*\\)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));


    /// <summary>
    /// 检查全部已声明分区实体并保证当前写入范围及三个后续范围存在
    /// </summary>
    /// <param name="cancellationToken">取消任务的令牌</param>
    public async Task EnsurePartitionsAsync(CancellationToken cancellationToken = default)
    {

        var definitions = GetPartitionDefinitions();
        if (definitions.Count == 0)
        {
            return;
        }

        await using var lockHandle = await distributedLock.TryLockAsync(LockName, LockLease, cancellationToken: cancellationToken);
        if (lockHandle is null)
        {
            logger.LogInformation("另一个实例正在维护 PostgreSQL 分区，本轮维护跳过");
            return;
        }

        using var lockLostCancellation = new CancellationTokenSource();
        using var renewalCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lockLostCancellation.Token);
        var operationCancellationToken = operationCancellation.Token;
        var renewalTask = RenewLockAsync(lockHandle, renewalCancellation.Token, lockLostCancellation);

        var connection = db.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;

        try
        {
            try
            {
                if (shouldCloseConnection)
                {
                    await connection.OpenAsync(operationCancellationToken);
                }

                var utcNow = DateTimeOffset.UtcNow;
                foreach (var definition in definitions)
                {
                    await EnsureTableAsync(connection, definition, utcNow, operationCancellationToken);
                }

                operationCancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    await connection.CloseAsync();
                }
            }
        }
        catch (OperationCanceledException exception) when (lockLostCancellation.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("PostgreSQL 分区维护期间分布式锁续期失败，已取消本轮维护", exception);
        }
        finally
        {
            CancelSafely(renewalCancellation, "停止 PostgreSQL 分区维护锁续期时发生异常");
            await renewalTask;
        }

    }


    /// <summary>
    /// 定期续期分区维护锁并在失锁时取消数据库操作
    /// </summary>
    /// <param name="lockHandle">当前持有的分布式锁句柄</param>
    /// <param name="stoppingToken">停止续期的令牌</param>
    /// <param name="lockLostCancellation">锁失效后用于取消维护操作的令牌源</param>
    private async Task RenewLockAsync(IDistributedLockHandle lockHandle, CancellationToken stoppingToken, CancellationTokenSource lockLostCancellation)
    {

        try
        {
            using var timer = new PeriodicTimer(LockRenewalInterval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (await distributedLock.RenewAsync(lockHandle, LockLease, stoppingToken))
                {
                    continue;
                }

                CancelSafely(lockLostCancellation, "取消失锁后的 PostgreSQL 分区维护时发生异常");
                LogErrorSafely(null, "PostgreSQL 分区维护分布式锁续期失败，正在取消本轮维护");
                return;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            CancelSafely(lockLostCancellation, "取消续期异常后的 PostgreSQL 分区维护时发生异常");
            LogErrorSafely(exception, "PostgreSQL 分区维护分布式锁续期异常，正在取消本轮维护");
        }

    }


    /// <summary>
    /// 取消令牌源并防止回调异常破坏锁生命周期清理
    /// </summary>
    /// <param name="cancellationTokenSource">需要取消的令牌源</param>
    /// <param name="errorMessage">取消回调异常时使用的日志消息</param>
    private void CancelSafely(CancellationTokenSource cancellationTokenSource, string errorMessage)
    {

        try
        {
            cancellationTokenSource.Cancel();
        }
        catch (Exception exception)
        {
            LogErrorSafely(exception, errorMessage);
        }

    }


    /// <summary>
    /// 记录锁生命周期错误并防止日志提供程序异常影响维护状态
    /// </summary>
    /// <param name="exception">需要记录的异常</param>
    /// <param name="message">日志消息</param>
    private void LogErrorSafely(Exception? exception, string message)
    {

        try
        {
            if (exception is null)
            {
                logger.LogError(message);
            }
            else
            {
                logger.LogError(exception, message);
            }
        }
        catch
        {
        }

    }


    /// <summary>
    /// 从当前 DatabaseContext 模型读取全部分区表定义
    /// </summary>
    /// <returns>去重后的分区表定义</returns>
    private List<PartitionTableDefinition> GetPartitionDefinitions()
    {

        var definitions = new Dictionary<string, PartitionTableDefinition>(StringComparer.Ordinal);

        foreach (var entityType in db.Model.GetEntityTypes())
        {
            if (!PartitionTableDefinition.TryCreate(entityType, out var definition) || definition is null)
            {
                continue;
            }

            var identity = (definition.Schema ?? string.Empty) + "\n" + definition.TableName;
            if (definitions.TryGetValue(identity, out var existingDefinition))
            {
                if (existingDefinition.KeyColumnName != definition.KeyColumnName
                    || existingDefinition.Interval != definition.Interval
                    || existingDefinition.Unit != definition.Unit)
                {
                    throw new InvalidOperationException($"表 {definition.TableName} 的多个实体映射包含不一致的分区定义");
                }

                continue;
            }

            definitions.Add(identity, definition);
        }

        return [.. definitions.Values];

    }


    /// <summary>
    /// 检查单张父表并创建缺少的必要子分区
    /// </summary>
    /// <param name="connection">已打开的数据库连接</param>
    /// <param name="definition">分区表定义</param>
    /// <param name="utcNow">本轮维护统一使用的 UTC 时间</param>
    /// <param name="cancellationToken">取消任务的令牌</param>
    private async Task EnsureTableAsync(DbConnection connection, PartitionTableDefinition definition, DateTimeOffset utcNow, CancellationToken cancellationToken)
    {

        var parent = await LoadParentAsync(connection, definition, cancellationToken);
        ValidateParent(definition, parent);

        var existingPartitions = await LoadPartitionsAsync(connection, parent, cancellationToken);
        ValidateRanges(definition, existingPartitions);

        var requiredRanges = BuildRequiredRanges(definition, existingPartitions, utcNow);
        if (requiredRanges.Count > MaxCreateCountPerTable)
        {
            throw new InvalidOperationException($"分区表 {parent.Schema}.{parent.Name} 本次需要创建 {requiredRanges.Count} 个子分区，超过安全上限 {MaxCreateCountPerTable}");
        }

        foreach (var range in requiredRanges)
        {
            await CreatePartitionAsync(connection, definition, parent, existingPartitions, range, cancellationToken);
        }

    }


    /// <summary>
    /// 查询并验证 PostgreSQL 分区父表信息
    /// </summary>
    /// <param name="connection">已打开的数据库连接</param>
    /// <param name="definition">EF Core 分区表定义</param>
    /// <param name="cancellationToken">取消任务的令牌</param>
    /// <returns>数据库中的分区父表信息</returns>
    private static async Task<PartitionParent> LoadParentAsync(DbConnection connection, PartitionTableDefinition definition, CancellationToken cancellationToken)
    {

        const string sql = """
            SELECT parent_namespace.nspname,
                   parent.relname,
                   partitioned.partstrat::text,
                   partitioned.partnatts,
                   partitioned.partexprs IS NULL,
                   partition_attribute.attname
            FROM pg_catalog.pg_partitioned_table AS partitioned
            INNER JOIN pg_catalog.pg_class AS parent ON parent.oid = partitioned.partrelid
            INNER JOIN pg_catalog.pg_namespace AS parent_namespace ON parent_namespace.oid = parent.relnamespace
            LEFT JOIN LATERAL unnest(partitioned.partattrs::smallint[]) WITH ORDINALITY AS partition_key(attnum, position)
                ON partition_key.position = 1
            LEFT JOIN pg_catalog.pg_attribute AS partition_attribute
                ON partition_attribute.attrelid = parent.oid
                AND partition_attribute.attnum = partition_key.attnum
            WHERE parent_namespace.nspname::text = COALESCE(CAST(@schema AS text), current_schema())
              AND parent.relname::text = @tableName;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "schema", definition.Schema);
        AddParameter(command, "tableName", definition.TableName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException($"表 {definition.Schema ?? "<current_schema>"}.{definition.TableName} 不存在或不是 PostgreSQL 分区父表");
        }

        return new PartitionParent(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt16(3),
            reader.GetBoolean(4),
            reader.IsDBNull(5) ? null : reader.GetString(5));

    }


    /// <summary>
    /// 验证数据库父表的策略和分区键与 EF Core 模型一致
    /// </summary>
    /// <param name="definition">EF Core 分区表定义</param>
    /// <param name="parent">数据库中的父表信息</param>
    private static void ValidateParent(PartitionTableDefinition definition, PartitionParent parent)
    {

        if (!string.Equals(parent.Strategy, "r", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"表 {parent.Schema}.{parent.Name} 不是 RANGE 分区表");
        }

        if (parent.KeyColumnCount != 1 || !parent.UsesDirectColumns || !string.Equals(parent.KeyColumnName, definition.KeyColumnName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"表 {parent.Schema}.{parent.Name} 的实际分区键与模型列 {definition.KeyColumnName} 不一致");
        }

    }


    /// <summary>
    /// 查询父表当前挂载的全部直接子分区和实际范围
    /// </summary>
    /// <param name="connection">已打开的数据库连接</param>
    /// <param name="parent">数据库中的父表信息</param>
    /// <param name="cancellationToken">取消任务的令牌</param>
    /// <returns>按起始边界排序的子分区</returns>
    private static async Task<List<ExistingPartition>> LoadPartitionsAsync(DbConnection connection, PartitionParent parent, CancellationToken cancellationToken)
    {

        const string sql = """
            SELECT child_namespace.nspname,
                   child.relname,
                   pg_catalog.pg_get_expr(child.relpartbound, child.oid)
            FROM pg_catalog.pg_inherits AS inheritance
            INNER JOIN pg_catalog.pg_class AS parent ON parent.oid = inheritance.inhparent
            INNER JOIN pg_catalog.pg_namespace AS parent_namespace ON parent_namespace.oid = parent.relnamespace
            INNER JOIN pg_catalog.pg_class AS child ON child.oid = inheritance.inhrelid
            INNER JOIN pg_catalog.pg_namespace AS child_namespace ON child_namespace.oid = child.relnamespace
            WHERE parent_namespace.nspname::text = @schema
              AND parent.relname::text = @tableName;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "schema", parent.Schema);
        AddParameter(command, "tableName", parent.Name);

        var partitions = new List<ExistingPartition>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var boundExpression = reader.GetString(2);
            var match = PartitionBoundRegex.Match(boundExpression);
            if (!match.Success
                || !long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var startId)
                || !long.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var endId))
            {
                throw new InvalidOperationException($"子分区 {reader.GetString(0)}.{reader.GetString(1)} 的边界 {boundExpression} 不是受支持的有限 bigint RANGE 范围");
            }

            partitions.Add(new ExistingPartition(reader.GetString(0), reader.GetString(1), startId, endId));
        }

        return [.. partitions.OrderBy(partition => partition.StartId)];

    }


    /// <summary>
    /// 验证已有子分区边界合法且彼此不重叠
    /// </summary>
    /// <param name="definition">分区表定义</param>
    /// <param name="partitions">已有子分区</param>
    private static void ValidateRanges(PartitionTableDefinition definition, List<ExistingPartition> partitions)
    {

        ExistingPartition? previous = null;
        foreach (var partition in partitions)
        {
            if (partition.StartId >= partition.EndId)
            {
                throw new InvalidOperationException($"分区表 {definition.TableName} 的子分区 {partition.Name} 边界无效");
            }

            if (!IsSnowflakeMillisecondBoundary(partition.StartId) || !IsSnowflakeMillisecondBoundary(partition.EndId))
            {
                throw new InvalidOperationException($"分区表 {definition.TableName} 的子分区 {partition.Name} 边界 [{partition.StartId}, {partition.EndId}) 不是完整毫秒对应的最小雪花 ID");
            }

            if (previous is not null && partition.StartId < previous.EndId)
            {
                throw new InvalidOperationException($"分区表 {definition.TableName} 的子分区 {previous.Name} 与 {partition.Name} 范围重叠");
            }

            previous = partition;
        }

    }


    /// <summary>
    /// 判断雪花 ID 是否为某个完整毫秒对应的最小值
    /// </summary>
    /// <param name="id">待验证的雪花 ID</param>
    /// <returns>低位全部为零且能够还原到同一边界时返回 true</returns>
    private static bool IsSnowflakeMillisecondBoundary(long id)
    {

        return SnowflakeIdLayout.GetMinIdByTime(SnowflakeIdLayout.GetTimeById(id)) == id;

    }


    /// <summary>
    /// 根据数据库实际最右边界计算恢复当前写入并预建三个后续分区所需的范围
    /// </summary>
    /// <param name="definition">分区表定义</param>
    /// <param name="partitions">已有子分区</param>
    /// <param name="utcNow">当前 UTC 时间</param>
    /// <returns>需要按顺序创建的范围</returns>
    private static List<PartitionRange> BuildRequiredRanges(PartitionTableDefinition definition, List<ExistingPartition> partitions, DateTimeOffset utcNow)
    {

        var currentId = SnowflakeIdLayout.GetMinIdByTime(utcNow);

        if (partitions.Count == 0)
        {
            var currentRange = definition.CreateInitialRange(utcNow);
            var initialRanges = new List<PartitionRange> { currentRange };
            AppendFutureRanges(definition, initialRanges, currentRange.EndId);
            return initialRanges;
        }

        var currentPartition = partitions.FirstOrDefault(partition => partition.StartId <= currentId && currentId < partition.EndId);
        if (currentPartition is not null)
        {
            return BuildMissingFutureRanges(definition, partitions, currentPartition.EndId);
        }

        var rightmostPartition = partitions[^1];
        if (rightmostPartition.EndId > currentId)
        {
            throw new InvalidOperationException($"分区表 {definition.TableName} 的当前雪花 ID 未被覆盖，但数据库中已经存在更晚的子分区");
        }

        var requiredRanges = new List<PartitionRange>();
        var nextRange = definition.CreateNextRange(rightmostPartition.EndId);

        while (true)
        {
            requiredRanges.Add(nextRange);
            if (requiredRanges.Count > MaxCreateCountPerTable)
            {
                return requiredRanges;
            }

            if (nextRange.StartId <= currentId && currentId < nextRange.EndId)
            {
                AppendFutureRanges(definition, requiredRanges, nextRange.EndId);
                return requiredRanges;
            }

            nextRange = definition.CreateNextRange(nextRange.EndId);
        }

    }


    /// <summary>
    /// 从当前分区结束边界开始补足三个连续的未来子分区
    /// </summary>
    /// <param name="definition">分区表定义</param>
    /// <param name="partitions">已有子分区</param>
    /// <param name="startId">当前分区结束边界</param>
    /// <returns>需要创建的未来范围</returns>
    private static List<PartitionRange> BuildMissingFutureRanges(PartitionTableDefinition definition, List<ExistingPartition> partitions, long startId)
    {

        var requiredRanges = new List<PartitionRange>();
        var nextStartId = startId;

        for (var index = 0; index < FuturePartitionCount; index++)
        {
            var existingPartition = partitions.FirstOrDefault(partition => partition.StartId == nextStartId);
            if (existingPartition is not null)
            {
                nextStartId = existingPartition.EndId;
                continue;
            }

            if (partitions.Any(partition => partition.StartId > nextStartId))
            {
                throw new InvalidOperationException($"分区表 {definition.TableName} 的当前分区之后存在范围空洞，无法安全预建后续分区");
            }

            var range = definition.CreateNextRange(nextStartId);
            requiredRanges.Add(range);
            nextStartId = range.EndId;
        }

        return requiredRanges;

    }


    /// <summary>
    /// 从指定边界连续追加三个未来子分区范围
    /// </summary>
    /// <param name="definition">分区表定义</param>
    /// <param name="ranges">需要追加范围的集合</param>
    /// <param name="startId">首个未来分区的起始边界</param>
    private static void AppendFutureRanges(PartitionTableDefinition definition, List<PartitionRange> ranges, long startId)
    {

        var nextStartId = startId;

        for (var index = 0; index < FuturePartitionCount; index++)
        {
            var range = definition.CreateNextRange(nextStartId);
            ranges.Add(range);
            nextStartId = range.EndId;
        }

    }


    /// <summary>
    /// 幂等校验后创建单个子分区
    /// </summary>
    /// <param name="connection">已打开的数据库连接</param>
    /// <param name="definition">分区表定义</param>
    /// <param name="parent">数据库父表</param>
    /// <param name="existingPartitions">本轮已知并持续更新的子分区</param>
    /// <param name="range">期望范围</param>
    /// <param name="cancellationToken">取消任务的令牌</param>
    private async Task CreatePartitionAsync(DbConnection connection, PartitionTableDefinition definition, PartitionParent parent, List<ExistingPartition> existingPartitions, PartitionRange range, CancellationToken cancellationToken)
    {

        var partitionName = PartitionNameBuilder.Create(parent.Name, range.StartTime);
        var sameName = existingPartitions.FirstOrDefault(partition => string.Equals(partition.Name, partitionName, StringComparison.Ordinal));
        if (sameName is not null)
        {
            if (sameName.StartId == range.StartId && sameName.EndId == range.EndId && string.Equals(sameName.Schema, parent.Schema, StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException($"子分区名称 {parent.Schema}.{partitionName} 已存在，但边界或父表与期望不一致");
        }

        var sameRange = existingPartitions.FirstOrDefault(partition => partition.StartId == range.StartId && partition.EndId == range.EndId);
        if (sameRange is not null)
        {
            throw new InvalidOperationException($"期望范围 [{range.StartId}, {range.EndId}) 已由名称不同的子分区 {sameRange.Schema}.{sameRange.Name} 覆盖");
        }

        var sql = $"CREATE TABLE {QuoteIdentifier(parent.Schema)}.{QuoteIdentifier(partitionName)} PARTITION OF {QuoteIdentifier(parent.Schema)}.{QuoteIdentifier(parent.Name)} FOR VALUES FROM ({range.StartId.ToString(CultureInfo.InvariantCulture)}) TO ({range.EndId.ToString(CultureInfo.InvariantCulture)});";

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (DbException)
        {
            var refreshedPartitions = await LoadPartitionsAsync(connection, parent, cancellationToken);
            var refreshedPartition = refreshedPartitions.FirstOrDefault(partition => string.Equals(partition.Name, partitionName, StringComparison.Ordinal));
            if (refreshedPartition is null || refreshedPartition.StartId != range.StartId || refreshedPartition.EndId != range.EndId)
            {
                throw;
            }

            existingPartitions.Clear();
            existingPartitions.AddRange(refreshedPartitions);
            return;
        }

        existingPartitions.Add(new ExistingPartition(parent.Schema, partitionName, range.StartId, range.EndId));
        existingPartitions.Sort((left, right) => left.StartId.CompareTo(right.StartId));

        logger.LogInformation(
            "已创建 PostgreSQL 子分区 {Schema}.{Partition}，父表 {Parent}，策略 {Interval} {Unit}，范围 [{StartId}, {EndId})",
            parent.Schema,
            partitionName,
            parent.Name,
            definition.Interval,
            definition.Unit,
            range.StartId,
            range.EndId);

    }


    /// <summary>
    /// 为数据库命令增加参数并正确处理 null
    /// </summary>
    /// <param name="command">数据库命令</param>
    /// <param name="name">参数名称</param>
    /// <param name="value">参数值</param>
    private static void AddParameter(DbCommand command, string name, object? value)
    {

        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;

        if (value is null or string)
        {
            parameter.DbType = DbType.String;
        }

        command.Parameters.Add(parameter);

    }


    /// <summary>
    /// 引用 PostgreSQL 标识符
    /// </summary>
    /// <param name="identifier">标识符原始值</param>
    /// <returns>双引号引用后的标识符</returns>
    private static string QuoteIdentifier(string identifier)
    {

        return '"' + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';

    }


    /// <summary>
    /// 保存数据库中的分区父表信息
    /// </summary>
    /// <param name="Schema">父表 Schema</param>
    /// <param name="Name">父表名称</param>
    /// <param name="Strategy">PostgreSQL 分区策略代码</param>
    /// <param name="KeyColumnCount">分区键列数量</param>
    /// <param name="UsesDirectColumns">分区键是否只使用直接列</param>
    /// <param name="KeyColumnName">直接分区键列名称</param>
    private sealed record PartitionParent(string Schema, string Name, string Strategy, short KeyColumnCount, bool UsesDirectColumns, string? KeyColumnName);


    /// <summary>
    /// 保存数据库中的已有子分区范围
    /// </summary>
    /// <param name="Schema">子分区 Schema</param>
    /// <param name="Name">子分区名称</param>
    /// <param name="StartId">包含的起始雪花 ID</param>
    /// <param name="EndId">不包含的结束雪花 ID</param>
    private sealed record ExistingPartition(string Schema, string Name, long StartId, long EndId);

}
