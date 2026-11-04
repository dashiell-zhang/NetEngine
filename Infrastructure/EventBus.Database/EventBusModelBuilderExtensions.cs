using EventBus.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace EventBus.Database;

/// <summary>
/// 将广播组件的内部实体接入已有上下文模型
/// </summary>
public static class EventBusModelBuilderExtensions
{

    /// <summary>
    /// 配置事件记录与提交位置，不改变业务实体的映射
    /// </summary>
    public static ModelBuilder ConfigureEventBus(this ModelBuilder modelBuilder)
    {

        var stream = modelBuilder.Entity<EventBusStream>();
        stream.ToTable("EventBusStream");
        stream.HasKey(t => t.Scope);
        stream.Property(t => t.Scope).HasMaxLength(200);
        stream.Property(t => t.Position).ValueGeneratedNever();
        stream.Property(t => t.Version).IsConcurrencyToken().ValueGeneratedNever();

        var message = modelBuilder.Entity<EventBusMessage>();
        message.ToTable("EventBusMessage");
        message.HasKey(t => new { t.Scope, t.Position });
        message.Property(t => t.Scope).HasMaxLength(200);
        message.Property(t => t.Position).ValueGeneratedNever();
        message.Property(t => t.EventName).HasMaxLength(1024);
        message.HasIndex(t => new { t.Scope, t.PublishedAt });
        return modelBuilder;

    }

}
