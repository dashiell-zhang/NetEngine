using Microsoft.Extensions.Logging;

namespace TaskService.Core;

internal static class InitTaskBackgroundServiceExtensions
{

    public static async Task WaitForInitializationAsync(this InitTaskBackgroundService initTaskBackgroundService, CancellationToken cancellationToken)
    {
        if (initTaskBackgroundService == null)
        {
            throw new ArgumentNullException(nameof(initTaskBackgroundService));
        }

        while (initTaskBackgroundService.ExecuteTask is null)
        {
            await Task.Delay(50, cancellationToken);
        }

        await initTaskBackgroundService.ExecuteTask.WaitAsync(cancellationToken);
    }


    /// <summary>
    /// 等待初始化完成，取消或失败时返回 false 并记录初始化异常
    /// </summary>
    public static async Task<bool> TryWaitForInitializationAsync(this InitTaskBackgroundService initTaskBackgroundService, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            await initTaskBackgroundService.WaitForInitializationAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "InitTaskBackgroundService 初始化失败");
            return false;
        }
    }

}
