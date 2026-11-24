using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace EventBus.Redis;

/// <summary>
/// 注册使用明确 Redis 目标的广播后端，与业务缓存和锁独立绑定
/// </summary>
public static class ServiceCollectionExtensions
{

    private static readonly object ConnectionKey = new();


    /// <summary>
    /// 接入 Redis Streams 事件广播
    /// </summary>
    public static IServiceCollection AddRedisEventBus(this IServiceCollection services, string connectionString, Action<EventBusOptions>? configure = null)
    {

        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var configuration = ConfigurationOptions.Parse(connectionString);
        if (configuration.EndPoints.Count == 0)
        {
            throw new ArgumentException("EventBus 的 Redis 连接串缺少服务器地址", nameof(connectionString));
        }

        services.AddKeyedSingleton<IConnectionMultiplexer>(ConnectionKey, (_, _) => ConnectionMultiplexer.Connect(configuration));
        services.AddSingleton(provider => new RedisEventHealthCheck(() => provider.GetRequiredKeyedService<IConnectionMultiplexer>(ConnectionKey)));
        return services.AddEventBus<RedisEventTransport>(configure, provider => new RedisEventTransport(
            provider.GetRequiredKeyedService<IConnectionMultiplexer>(ConnectionKey),
            provider.GetRequiredService<IOptions<EventBusOptions>>(),
            provider.GetRequiredService<ILogger<RedisEventTransport>>()));

    }

}
