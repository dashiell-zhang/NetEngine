using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace Repository.Partitioning;

/// <summary>
/// 根据启动快照修正分区命令的 xmin 读写标记，SQL 和结果传播仍由提供程序完成
/// </summary>
internal sealed class PartitionXminModificationCommandFactory(IModificationCommandFactory providerFactory) : IModificationCommandFactory
{

    /// <summary>
    /// 装饰跟踪命令，等条目齐全后从所属上下文读取快照，单例工厂不保存数据库状态
    /// </summary>
    public IModificationCommand CreateModificationCommand(in ModificationCommandParameters parameters)
    {

        var command = providerFactory.CreateModificationCommand(parameters);
        return new PartitionCommand(command);

    }


    /// <summary>
    /// 非跟踪命令完全沿用提供程序实现
    /// </summary>
    public INonTrackedModificationCommand CreateNonTrackedModificationCommand(in NonTrackedModificationCommandParameters parameters) => providerFactory.CreateNonTrackedModificationCommand(parameters);


    /// <summary>
    /// 根据实际行操作修正列标记，不修改实体状态、原始版本或命令文本
    /// </summary>
    private sealed class PartitionCommand(IModificationCommand command) : IModificationCommand
    {

        /// <summary>
        /// 原始物理表元数据
        /// </summary>
        public ITable? Table => command.Table;


        /// <summary>
        /// 原始存储过程映射
        /// </summary>
        public IStoreStoredProcedure? StoreStoredProcedure => command.StoreStoredProcedure;


        /// <summary>
        /// 原始物理表名称
        /// </summary>
        public string TableName => command.TableName;


        /// <summary>
        /// 原始物理表架构
        /// </summary>
        public string? Schema => command.Schema;


        /// <summary>
        /// 原始跟踪条目，保留 EF 共享行身份和关联查找
        /// </summary>
        public IReadOnlyList<IUpdateEntry> Entries => command.Entries;


        /// <summary>
        /// 提供程序确定的实际插入更新删除操作
        /// </summary>
        public EntityState EntityState => command.EntityState;


        /// <summary>
        /// 原始受影响行数输出列
        /// </summary>
        public IColumnBase? RowsAffectedColumn => command.RowsAffectedColumn;


        /// <summary>
        /// 分区插入不返回 xmin，分区更新保留并发条件及返回值，普通表保持提供程序行为
        /// </summary>
        public IReadOnlyList<IColumnModification> ColumnModifications
        {
            get
            {
                var columns = command.ColumnModifications;
                var snapshot = Entries.Count == 0 ? null
                    : Entries[0].Context.GetService<IDbContextOptions>().FindExtension<PartitionXminOptionsExtension>()?.Snapshot;
                if (StoreStoredProcedure == null && snapshot?.Contains(TableName, Schema) == true)
                {
                    foreach (var column in columns)
                    {
                        if (column.ColumnName != "xmin" || column.Property is not { IsConcurrencyToken: true } property
                            || (Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) != typeof(uint)) continue;
                        column.IsWrite = false;
                        column.IsRead = EntityState == EntityState.Modified;
                    }
                }
                return columns;
            }
        }


        /// <summary>
        /// 由原命令维护主实体及共享实体顺序
        /// </summary>
        public void AddEntry(IUpdateEntry entry, bool mainEntry) => command.AddEntry(entry, mainEntry);


        /// <summary>
        /// 由提供程序将真实返回版本传播到原始跟踪条目
        /// </summary>
        public void PropagateResults(RelationalDataReader relationalReader) => command.PropagateResults(relationalReader);


        /// <summary>
        /// 保留原始输出参数传播行为
        /// </summary>
        public void PropagateOutputParameters(DbParameterCollection parameterCollection, int baseParameterIndex) => command.PropagateOutputParameters(parameterCollection, baseParameterIndex);

    }

}
