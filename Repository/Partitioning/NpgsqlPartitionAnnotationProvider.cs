#pragma warning disable EF1001

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.Internal;

namespace Repository.Partitioning;

/// <summary>
/// 在保留 Npgsql Annotation 的同时把实体分区元数据投影到关系表
/// </summary>
public sealed class NpgsqlPartitionAnnotationProvider : NpgsqlAnnotationProvider
{

    /// <summary>
    /// 创建 PostgreSQL 分区 Annotation 提供器
    /// </summary>
    /// <param name="dependencies">关系 Annotation 提供器依赖</param>
    public NpgsqlPartitionAnnotationProvider(RelationalAnnotationProviderDependencies dependencies) : base(dependencies)
    {

    }


    /// <summary>
    /// 获取关系表需要写入 Migration 操作的 Annotation
    /// </summary>
    /// <param name="table">关系表</param>
    /// <param name="designTime">是否为设计时模型</param>
    /// <returns>Npgsql 和分区表 Annotation</returns>
    public override IEnumerable<IAnnotation> For(ITable table, bool designTime)
    {

        foreach (var annotation in base.For(table, designTime))
        {
            yield return annotation;
        }

        foreach (var name in PartitionAnnotationNames.GetAll())
        {
            object? value = null;

            foreach (var mapping in table.EntityTypeMappings)
            {
                var mappingValue = mapping.TypeBase.FindAnnotation(name)?.Value;
                if (mappingValue is null)
                {
                    continue;
                }

                if (value is not null && !Equals(value, mappingValue))
                {
                    throw new InvalidOperationException($"表 {table.Schema}.{table.Name} 的多个实体映射包含不一致的分区 Annotation {name}");
                }

                value = mappingValue;
            }

            if (value is not null)
            {
                yield return new Annotation(name, value);
            }
        }

    }

}
