using DistributedLock;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Repository.Core.Partitioning;

namespace Repository.Core.Extensions;

/// <summary>
/// 为选定数据库注册独立的分区维护服务
/// </summary>
public static class PartitionServiceCollectionExtensions
{

    /// <summary>
    /// 将分区维护绑定到具体上下文、共享数据库标识和锁实例
    /// </summary>
    public static IServiceCollection AddPartitionMaintenance<TContext>(this IServiceCollection services, string databaseIdentity, object? lockServiceKey = null) where TContext : DbContext
    {

        ArgumentException.ThrowIfNullOrWhiteSpace(databaseIdentity);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(PartitionMaintenanceService<TContext>)))
        {
            throw new InvalidOperationException($"上下文 {typeof(TContext).Name} 已注册分区维护");
        }

        services.AddScoped(provider => new PartitionMaintenanceService<TContext>(
            provider.GetRequiredService<TContext>(),
            lockServiceKey is null ? provider.GetRequiredService<IDistributedLock>() : provider.GetRequiredKeyedService<IDistributedLock>(lockServiceKey),
            provider.GetRequiredService<ILoggerFactory>().CreateLogger($"Repository.Core.Partitioning.Maintenance.{typeof(TContext).FullName}"), databaseIdentity));
        services.AddHostedService(provider => new PartitionMaintenanceBackgroundService<TContext>(provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILoggerFactory>().CreateLogger($"Repository.Core.Partitioning.Background.{typeof(TContext).FullName}")));
        return services;

    }

}
