using EventBus.Database.Database.Generated;
using Microsoft.EntityFrameworkCore;
using Repository.Core.Bases;
using Repository.Core.Extensions;

namespace EventBus.Database.Persistence;

/// <summary>
/// 独立管理广播位置和事件记录，不包含业务实体或分区能力
/// </summary>
public sealed class EventBusDatabaseContext(DbContextOptions<EventBusDatabaseContext> options) : RepositoryDbContext(options)
{

    /// <summary>
    /// 事件模型在 public 中使用的迁移历史表
    /// </summary>
    public const string MigrationHistoryTable = "__EFMigrationsHistory_EventBus";


    /// <summary>
    /// 广播范围及已提交位置
    /// </summary>
    public DbSet<EventBusStream> Streams => Set<EventBusStream>();


    /// <summary>
    /// 可供实例读取的广播记录
    /// </summary>
    public DbSet<EventBusMessage> Messages => Set<EventBusMessage>();


    /// <summary>
    /// 配置独立主键、无条件唯一约束、软删除及普通表 xmin
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.ConfigureEventBus();
        modelBuilder.ApplySoftDeleteFilters(this);
        modelBuilder.ApplyModelDocumentation(this);

    }

}
