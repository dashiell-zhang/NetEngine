using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace QueueTask.Database;

/// <summary>
/// 按具体业务上下文接入本地队列能力
/// </summary>
public static class QueueTaskServiceCollectionExtensions
{

    /// <summary>
    /// 注册模块入队能力，不启动任务执行器或选择全局默认业务库
    /// </summary>
    public static IServiceCollection AddQueueTaskDatabase<TContext>(this IServiceCollection services) where TContext : DbContext
    {

        services.TryAddScoped<QueueTaskProducer<TContext>>();
        services.TryAddSingleton<QueueTaskStore<TContext>>();
        services.TryAddSingleton<QueueTaskManagement<TContext>>();
        return services;

    }

}
