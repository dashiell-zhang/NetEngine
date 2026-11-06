using DistributedLock.Redis.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DistributedLock.Redis;

/// <summary>
/// 注册配置固定且彼此隔离的 Redis 锁实例
/// </summary>
public static class ServiceCollectionExtensions
{

    /// <summary>
    /// 注册宿主默认锁，不允许覆盖已有默认绑定
    /// </summary>
    public static void AddRedisLock(this IServiceCollection services, Action<RedisSetting> action)
    {

        AddRedisLock(services, null, action);

    }


    /// <summary>
    /// 为共享分区维护等专用协调用途注册命名锁
    /// </summary>
    public static void AddRedisLock(this IServiceCollection services, object? serviceKey, Action<RedisSetting> action)
    {

        if (services.Any(descriptor => descriptor.ServiceType == typeof(IDistributedLock) && Equals(descriptor.ServiceKey, serviceKey)))
        {
            throw new InvalidOperationException($"锁服务 {serviceKey ?? "默认"} 已注册");
        }

        var settings = new RedisSetting();
        action(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.Configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.InstanceName);
        var snapshot = new RedisSetting { Configuration = settings.Configuration, InstanceName = settings.InstanceName };
        services.AddKeyedSingleton<IDistributedLock>(serviceKey, (provider, _) => new RedisLock(snapshot, provider.GetService<ILogger<RedisLock>>()));

    }

}
