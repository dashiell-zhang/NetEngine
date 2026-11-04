using Microsoft.Extensions.DependencyInjection;

namespace EventBus.Redis;

/// <summary>
/// 注册复用现有 Redis 连接的广播后端
/// </summary>
public static class ServiceCollectionExtensions
{

    /// <summary>
    /// 接入 Redis Streams 事件广播
    /// </summary>
    public static IServiceCollection AddRedisEventBus(this IServiceCollection services, Action<EventBusOptions>? configure = null)
    {

        return services.AddEventBus<RedisEventTransport>(configure);

    }

}
