using Logger.LocalFile.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Logger.LocalFile.Tasks;

/// <summary>
/// 定期清理本组件过期小时文件，通过写入器协调文件访问
/// </summary>
internal class LogClearTask(IOptionsMonitor<LoggerSetting> config, LocalFileLogWriter logWriter) : BackgroundService
{

    private readonly int saveDays = config.CurrentValue.SaveDays;


    /// <summary>
    /// 按保留天数执行清理并响应停机取消，失败时保留文件并报告
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        if (saveDays == -1)
        {
            return;
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await logWriter.ClearExpiredFilesAsync(saveDays, stoppingToken);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    LogDiagnostics.ReportFailure("cleanup", exception);
                }

                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 停机取消不作为清理失败
        }

    }

}
