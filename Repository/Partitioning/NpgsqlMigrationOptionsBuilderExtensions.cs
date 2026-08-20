#pragma warning disable EF1001

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Repository.Partitioning;

/// <summary>
/// 提供 PostgreSQL 分区 Migration 服务替换入口
/// </summary>
public static class NpgsqlMigrationOptionsBuilderExtensions
{

    /// <summary>
    /// 启用 PostgreSQL 分区表 Migration Annotation 和 SQL 生成能力
    /// </summary>
    /// <param name="optionsBuilder">DbContext 选项构建器</param>
    /// <returns>当前 DbContext 选项构建器</returns>
    public static DbContextOptionsBuilder UsePostgreSqlPartitioning(this DbContextOptionsBuilder optionsBuilder)
    {

        optionsBuilder.ReplaceService<IRelationalAnnotationProvider, NpgsqlPartitionAnnotationProvider>();
        optionsBuilder.ReplaceService<IMigrationsSqlGenerator, NpgsqlPartitionMigrationsSqlGenerator>();
        return optionsBuilder;

    }

}
