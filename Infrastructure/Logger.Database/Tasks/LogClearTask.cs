using Logger.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Logger.Database.Persistence;

namespace Logger.Database.Tasks;

/// <summary>
/// 定期清理数据库中的过期日志，失败时报告并保留下一轮重试
/// </summary>
internal class LogClearTask(IOptionsMonitor<LoggerSetting> config, IDbContextFactory<LogDatabaseContext> contextFactory) : BackgroundService
{

    private readonly int saveDays = config.CurrentValue.SaveDays;

    private readonly string project = config.CurrentValue.Project;


    /// <summary>
    /// 按保留天数执行清理并响应停机取消，极大保留天数以最早日期为界
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
                    await using var db = await contextFactory.CreateDbContextAsync(stoppingToken);
                    var now = DateTimeOffset.UtcNow;
                    var deleteTime = saveDays > (now - DateTimeOffset.MinValue).TotalDays ? DateTimeOffset.MinValue : now.AddDays(-saveDays);

                    await db.Log.Where(t => t.Project == project && t.CreateTime <= deleteTime).ExecuteDeleteAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
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
