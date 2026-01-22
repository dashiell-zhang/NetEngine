using Microsoft.Extensions.DependencyInjection;

namespace DistributedLock.InMemory;

/// <summary>
/// 依赖注入扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册内存锁实现
    /// </summary>
    /// <param name="services">服务集合</param>
    public static void AddInMemoryLock(this IServiceCollection services)
    {
        services.AddSingleton<IDistributedLock, InMemoryLock>();
    }
}
