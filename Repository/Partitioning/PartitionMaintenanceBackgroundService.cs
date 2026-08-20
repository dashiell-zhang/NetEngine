using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Repository.Partitioning;

/// <summary>
/// 在宿主启动完成前检查 PostgreSQL 分区并按固定周期持续维护
/// </summary>
public sealed class PartitionMaintenanceBackgroundService(IServiceScopeFactory scopeFactory, ILogger<PartitionMaintenanceBackgroundService> logger) : BackgroundService
{

    /// <summary>
    /// 分区维护执行间隔
    /// </summary>
    private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromMinutes(10);


    /// <summary>
    /// 在宿主启动完成前执行首次分区维护
    /// </summary>
    /// <param name="cancellationToken">取消启动的令牌</param>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {

        await MaintainPartitionsAsync(cancellationToken);
        await base.StartAsync(cancellationToken);

    }


    /// <summary>
    /// 启动周期性分区维护循环
    /// </summary>
    /// <param name="stoppingToken">停止后台服务的令牌</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        using var timer = new PeriodicTimer(MaintenanceInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await MaintainPartitionsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

    }


    /// <summary>
    /// 在独立依赖注入作用域中执行一次分区维护
    /// </summary>
    /// <param name="cancellationToken">取消维护的令牌</param>
    private async Task MaintainPartitionsAsync(CancellationToken cancellationToken)
    {

        try
        {
            using var scope = scopeFactory.CreateScope();
            var maintenanceService = scope.ServiceProvider.GetRequiredService<PartitionMaintenanceService>();
            await maintenanceService.EnsurePartitionsAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "PostgreSQL 分区自动维护失败，将在下一周期重试");
        }

    }

}
