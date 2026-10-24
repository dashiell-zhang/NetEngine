using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace Repository.Partitioning;

/// <summary>
/// 提供 PostgreSQL 分区存储与 Migration 服务配置入口
/// </summary>
public static class NpgsqlMigrationOptionsBuilderExtensions
{

    /// <summary>
    /// 启用分区 xmin 存储及命令扩展、继承映射校验和分区表 Migration 能力
    /// </summary>
    /// <param name="optionsBuilder">DbContext 选项构建器</param>
    /// <param name="snapshot">启动时加载的写库分区快照，离线建模和迁移时可省略，保存数据时必须提供</param>
    /// <returns>当前 DbContext 选项构建器</returns>
    public static DbContextOptionsBuilder UsePostgreSqlPartitioning(this DbContextOptionsBuilder optionsBuilder, PartitionTableSnapshot? snapshot = null)
    {

        optionsBuilder.ReplaceService<IDatabase, PartitionXminDatabase>();
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(new PartitionXminOptionsExtension(snapshot));
        optionsBuilder.ReplaceService<IModelCustomizer, PartitionModelCustomizer>();
        // 仅旧分区建表组件的注册依赖提供程序内部类型，保存扩展仍接受 EF1001 检查
#pragma warning disable EF1001
        optionsBuilder.ReplaceService<IRelationalAnnotationProvider, NpgsqlPartitionAnnotationProvider>();
        optionsBuilder.ReplaceService<IMigrationsSqlGenerator, NpgsqlPartitionMigrationsSqlGenerator>();
#pragma warning restore EF1001
        return optionsBuilder;

    }

}
