using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Repository.Attributes;
using Repository.Bases;

namespace Repository.Partitioning;

/// <summary>
/// 将实体分区声明写入 EF Core 模型并校验数据库约束
/// </summary>
public static class PartitionModelBuilder
{

    /// <summary>
    /// 配置单个实体的 PostgreSQL 分区模型
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="modelBuilder">EF Core 模型构建器</param>
    /// <param name="interval">单个子分区包含的周期数量</param>
    /// <param name="unit">分区周期单位</param>
    public static void Configure<TEntity>(ModelBuilder modelBuilder, int interval, PartitionUnit unit) where TEntity : class
    {

        var entityBuilder = modelBuilder.Entity<TEntity>();
        var entityType = entityBuilder.Metadata;
        var entityName = entityType.ClrType.FullName ?? entityType.Name;
        var keyPropertyName = nameof(CD.Id);

        if (interval <= 0)
        {
            throw new InvalidOperationException($"实体 {entityName} 的分区周期数量必须大于 0");
        }

        if (!Enum.IsDefined(unit))
        {
            throw new InvalidOperationException($"实体 {entityName} 的分区周期单位 {unit} 不受支持");
        }

        var keyProperty = entityType.FindProperty(keyPropertyName) ?? throw new InvalidOperationException($"实体 {entityName} 不存在分区键属性 {keyPropertyName}");

        if (keyProperty.ClrType != typeof(long))
        {
            throw new InvalidOperationException($"实体 {entityName} 的分区键属性 {keyPropertyName} 必须是 long 类型");
        }

        var primaryKey = entityType.FindPrimaryKey() ?? throw new InvalidOperationException($"实体 {entityName} 没有主键，无法创建分区表");

        if (!primaryKey.Properties.Contains(keyProperty))
        {
            throw new InvalidOperationException($"实体 {entityName} 的主键必须包含分区键属性 {keyPropertyName}");
        }

        foreach (var key in entityType.GetKeys().Where(key => !ReferenceEquals(key, primaryKey)))
        {
            if (!key.Properties.Contains(keyProperty))
            {
                throw new InvalidOperationException($"实体 {entityName} 的唯一约束必须包含分区键属性 {keyPropertyName}");
            }
        }

        foreach (var index in entityType.GetIndexes().Where(index => index.IsUnique))
        {
            if (!index.Properties.Contains(keyProperty))
            {
                throw new InvalidOperationException($"实体 {entityName} 的唯一索引必须包含分区键属性 {keyPropertyName}");
            }
        }

        if (entityType.GetIsUnlogged())
        {
            throw new InvalidOperationException($"实体 {entityName} 是 PostgreSQL UNLOGGED 表，不能配置为分区父表");
        }

        var tableName = entityType.GetTableName() ?? throw new InvalidOperationException($"实体 {entityName} 没有映射到数据库表");
        var storeObject = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
        var keyColumnName = keyProperty.GetColumnName(storeObject) ?? throw new InvalidOperationException($"实体 {entityName} 的分区键属性 {keyPropertyName} 没有映射到当前表");

        entityBuilder.HasAnnotation(PartitionAnnotationNames.Strategy, PartitionAnnotationNames.RangeStrategy);
        entityBuilder.HasAnnotation(PartitionAnnotationNames.KeyColumn, keyColumnName);
        entityBuilder.HasAnnotation(PartitionAnnotationNames.KeyType, PartitionAnnotationNames.SnowflakeIdKeyType);
        entityBuilder.HasAnnotation(PartitionAnnotationNames.Interval, interval);
        entityBuilder.HasAnnotation(PartitionAnnotationNames.Unit, unit.ToString());

    }


    /// <summary>
    /// 在用户完成模型配置后校验继承策略，防止多表映射误用同表 xmin 回读
    /// </summary>
    internal static void ValidateInheritance(IReadOnlyEntityType entityType)
    {

        if (entityType.BaseType != null)
        {
            throw new InvalidOperationException($"分区声明必须配置在 EF 继承层级的根实体上，不能配置在派生实体 {entityType.Name} 上");
        }

        var strategy = entityType.GetMappingStrategy();
        if (strategy != null && strategy != RelationalAnnotationNames.TphMappingStrategy)
        {
            throw new InvalidOperationException($"分区实体 {entityType.Name} 不支持 {strategy} 继承映射，仅支持基类和派生类共用一张表的 TPH 映射");
        }

        if (entityType.GetDerivedTypes().Any(x => x.GetTableName() != entityType.GetTableName() || x.GetSchema() != entityType.GetSchema()))
        {
            throw new InvalidOperationException($"分区实体 {entityType.Name} 的继承层级必须映射到同一张表，不支持派生类映射到其他表");
        }

    }

}
