using EventBus.Database.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventBus.Database;

/// <summary>
/// 配置广播组件专用上下文的实体模型
/// </summary>
internal static class EventBusModelBuilderExtensions
{

    /// <summary>
    /// 配置事件记录与提交位置，不改变业务实体的映射
    /// </summary>
    public static ModelBuilder ConfigureEventBus(this ModelBuilder modelBuilder)
    {

        var stream = modelBuilder.Entity<EventBusStream>();
        stream.ToTable("EventBusStream");
        stream.HasKey(t => t.Id);
        stream.HasIndex(t => t.Scope).IsUnique();
        stream.Property(t => t.Scope).HasMaxLength(200);
        stream.Property(t => t.Position).ValueGeneratedNever();

        var message = modelBuilder.Entity<EventBusMessage>();
        message.ToTable("EventBusMessage");
        message.HasKey(t => t.Id);
        message.HasIndex(t => new { t.Scope, t.Position }).IsUnique();
        message.Property(t => t.Scope).HasMaxLength(200);
        message.Property(t => t.Position).ValueGeneratedNever();
        message.Property(t => t.EventName).HasMaxLength(1024);
        message.HasIndex(t => new { t.Scope, t.CreateTime });
        return modelBuilder;

    }

}
