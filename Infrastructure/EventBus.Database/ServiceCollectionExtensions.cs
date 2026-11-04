using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBus.Database;

/// <summary>
/// 通过已有主库上下文工厂接入数据库广播后端
/// </summary>
public static class ServiceCollectionExtensions
{

    /// <summary>
    /// 复用现有上下文配置并注册广播，不额外创建上下文工厂
    /// </summary>
    public static IServiceCollection AddDatabaseEventBus<TContext>(this IServiceCollection services, Action<EventBusOptions>? configure = null) where TContext : DbContext
    {

        return services.AddEventBus<DatabaseEventTransport<TContext>>(configure);

    }

}
