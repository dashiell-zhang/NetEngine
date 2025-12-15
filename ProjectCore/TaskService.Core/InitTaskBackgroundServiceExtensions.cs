using Microsoft.Extensions.Logging;

namespace TaskService.Core;

public static class InitTaskBackgroundServiceExtensions
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
            string errorMessage = "InitTaskBackgroundService 初始化失败";

            logger.LogError(ex, errorMessage);
            return false;
        }
    }

}
