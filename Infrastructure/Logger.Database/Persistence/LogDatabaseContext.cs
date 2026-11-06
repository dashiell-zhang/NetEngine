using Logger.Database.Database.Generated;
using Microsoft.EntityFrameworkCore;
using Repository.Core.Bases;
using Repository.Core.Extensions;

namespace Logger.Database.Persistence;

/// <summary>
/// 仅管理日志实体的写入上下文
/// </summary>
public class LogDatabaseContext : RepositoryDbContext
{

    /// <summary>
    /// 日志模型在 public 中使用的迁移历史表
    /// </summary>
    public const string MigrationHistoryTable = "__EFMigrationsHistory_Log";


    /// <summary>
    /// 创建日志写入上下文
    /// </summary>
    public LogDatabaseContext(DbContextOptions<LogDatabaseContext> options) : base(options)
    {

    }


    /// <summary>
    /// 创建复用日志模型的派生上下文
    /// </summary>
    protected LogDatabaseContext(DbContextOptions options) : base(options)
    {

    }


    /// <summary>
    /// 日志记录
    /// </summary>
    public DbSet<Log> Log => Set<Log>();


    /// <summary>
    /// 配置日志模型、软删除和月分区
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.ApplySoftDeleteFilters(this);
        modelBuilder.ApplyJsonColumns(this);
        modelBuilder.ApplyModelDocumentation(this);
        modelBuilder.ApplyPartitionTables(this);

    }

}
