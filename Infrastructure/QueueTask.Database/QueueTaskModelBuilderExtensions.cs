using Microsoft.EntityFrameworkCore;
using QueueTask.Database.Models;

namespace QueueTask.Database;

/// <summary>
/// 将本地队列与配置实体映射到业务模块模型
/// </summary>
public static class QueueTaskModelBuilderExtensions
{

    /// <summary>
    /// 配置任务表及有效配置唯一约束，带参数运行实例允许多条
    /// </summary>
    public static ModelBuilder ConfigureQueueTasks(this ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<Models.QueueTask>().ToTable("QueueTask");
        var setting = modelBuilder.Entity<TaskSetting>();
        setting.ToTable("TaskSetting");
        setting.HasIndex(item => new { item.Category, item.Name }).IsUnique()
            .HasFilter("\"DeleteTime\" IS NULL AND (\"Category\" = 'QueueTask' OR (\"Category\" = 'ScheduleTask' AND (\"Parameter\" IS NULL OR \"Parameter\" = '__args_default__')))");
        return modelBuilder;

    }

}
