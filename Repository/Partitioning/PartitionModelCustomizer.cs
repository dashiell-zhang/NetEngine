using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace Repository.Partitioning;

/// <summary>
/// 在 OnModelCreating 完成后校验分区映射并统一共享表的 xmin 配置
/// </summary>
public sealed class PartitionModelCustomizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
{

    /// <summary>
    /// 保留关系模型配置流程，校验继承并让共享分区行的版本使用相同生成策略
    /// </summary>
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {

        base.Customize(modelBuilder, context);
        var partitionTables = new HashSet<StoreObjectIdentifier>();
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.FindAnnotation(PartitionAnnotationNames.Strategy) != null)
            {
                PartitionModelBuilder.ValidateInheritance(entityType);
                partitionTables.Add(StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema()));
            }
        }

        var mappings = context.GetService<IRelationalTypeMappingSource>();
        var versionedTables = new HashSet<StoreObjectIdentifier>();
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var fragments = entityType.GetMappingFragments(StoreObjectType.Table);
            if (fragments.Any() && (entityType.GetTableName() is { } mainTable
                    && partitionTables.Contains(StoreObjectIdentifier.Table(mainTable, entityType.GetSchema()))
                || fragments.Any(fragment => partitionTables.Contains(fragment.StoreObject))))
            {
                throw new InvalidOperationException($"分区实体 {entityType.Name} 不支持 Entity Splitting（SplitToTable）映射，不能将同一实体拆到多张表；多个实体共用一张分区表的 Table Splitting 不受此限制");
            }
            if (entityType.GetTableName() is not { } name || entityType.IsMappedToJson()) continue;
            var table = StoreObjectIdentifier.Table(name, entityType.GetSchema());
            if (!partitionTables.Contains(table)) continue;
            foreach (var property in entityType.GetProperties())
            {
                if ((Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) != typeof(uint) || !property.IsConcurrencyToken) continue;
                // Npgsql 在模型最终化时才把未显式命名的 xid 行版本映射到 xmin
                var implicitXmin = property.ValueGenerated == ValueGenerated.OnAddOrUpdate
                    && property.FindAnnotation(RelationalAnnotationNames.ColumnName) == null
                    && mappings.FindMapping((IProperty)property)?.StoreType == "xid";
                if (property.GetColumnName(table) != "xmin" && !implicitXmin) continue;
                property.SetColumnName("xmin");
                property.SetColumnType("xid");
                property.ValueGenerated = ValueGenerated.OnAddOrUpdate;
                property.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
                property.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
                versionedTables.Add(table);
            }
        }

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.GetTableName() is not { } name || entityType.IsMappedToJson()) continue;
            var table = StoreObjectIdentifier.Table(name, entityType.GetSchema());
            if (!versionedTables.Contains(table) || entityType.GetProperties().Any(p => p.GetColumnName(table) == "xmin")) continue;
            // 提前补齐 EF 同表并发约定需要的影子版本，并明确禁止 INSERT 写入系统列
            var shadowName = "PartitionXmin";
            while (entityType.FindProperty(shadowName) != null) shadowName += "_";
            var version = entityType.AddProperty(shadowName, typeof(uint));
            version.SetColumnName("xmin");
            version.SetColumnType("xid");
            version.IsConcurrencyToken = true;
            version.ValueGenerated = ValueGenerated.OnAddOrUpdate;
            version.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            version.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        }

    }

}
