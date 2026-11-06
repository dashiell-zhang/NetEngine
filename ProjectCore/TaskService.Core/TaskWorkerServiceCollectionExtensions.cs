using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using global::QueueTask.Database;
using TaskService.Core.QueueTask;
using TaskService.Core.ScheduleTask;

namespace TaskService.Core;

/// <summary>
/// 显式选择 Worker 的业务库和任务类型，使用宿主默认并发锁
/// </summary>
public static class TaskWorkerServiceCollectionExtensions
{

    /// <summary>
    /// 注册单个业务库的执行器，不扫描其他已加载模块
    /// </summary>
    public static IServiceCollection AddTaskWorker<TContext>(this IServiceCollection services, IEnumerable<Type> taskTypes) where TContext : DbContext
    {

        if (services.Any(descriptor => descriptor.ServiceType == typeof(TaskWorkerSelection)))
        {
            throw new InvalidOperationException("一个任务宿主只能选择一个执行数据库");
        }

        var types = taskTypes.Distinct().ToArray();
        if (types.Any(type => !type.IsClass || type.IsAbstract || !type.IsPublic || !type.IsSubclassOf(typeof(TaskBase))))
        {
            throw new ArgumentException("任务类型必须是公开且具体的 TaskBase 子类", nameof(taskTypes));
        }

        services.AddSingleton(new TaskWorkerSelection(types));
        services.AddQueueTaskDatabase<TContext>();
        services.AddSingleton<IQueueTaskStore>(provider => provider.GetRequiredService<QueueTaskStore<TContext>>());
        services.AddScoped<IQueueTaskProducer>(provider => provider.GetRequiredService<QueueTaskProducer<TContext>>());
        services.AddScoped<QueueTaskService>();
        foreach (var type in types)
        {
            services.AddScoped(type);
        }

        services.AddHostedService<InitTaskBackgroundService>();
        services.AddHostedService<TaskSettingSyncBackgroundService>();
        services.AddHostedService<QueueTaskBackgroundService>();
        services.AddHostedService<ScheduleTaskBackgroundService>();
        return services;

    }


    /// <summary>
    /// 等待选定任务模块初始化，供宿主 Debug 交互使用
    /// </summary>
    public static Task WaitForTaskInitializationAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {

        return services.GetServices<IHostedService>().OfType<InitTaskBackgroundService>().Single().WaitForInitializationAsync(cancellationToken);

    }

}

/// <summary>
/// 保存本任务宿主显式选择的任务类型
/// </summary>
internal sealed class TaskWorkerSelection(IReadOnlyList<Type> taskTypes)
{

    /// <summary>
    /// 允许发现和同步配置的任务类型
    /// </summary>
    public IReadOnlyList<Type> TaskTypes { get; } = taskTypes;

}
