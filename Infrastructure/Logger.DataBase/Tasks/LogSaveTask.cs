using Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Repository.Database;
using System.Text;

namespace Logger.DataBase.Tasks;

internal class LogSaveTask(IServiceProvider serviceProvider) : BackgroundService
{

    private static readonly TimeSpan TmpRecoverAge = TimeSpan.FromSeconds(300);


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        while (!stoppingToken.IsCancellationRequested)
        {

            try
            {
                string basePath = Path.Combine(Directory.GetCurrentDirectory(), "Logs");

                if (Directory.Exists(basePath))
                {
                    RecoverTmpFiles(basePath);

                    List<string> logPaths = [.. Directory.EnumerateFiles(basePath, "batch-*.log").Take(50)];

                    if (logPaths.Count != 0)
                    {
                        using var scope = serviceProvider.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

                        foreach (var logPath in logPaths)
                        {
                            var logs = new List<TLog>();

                            using (var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous))
                            using (var reader = new StreamReader(stream, Encoding.UTF8))
                            {
                                while (!stoppingToken.IsCancellationRequested)
                                {
                                    var line = await reader.ReadLineAsync(stoppingToken);
                                    if (line == null)
                                    {
                                        break;
                                    }

                                    if (string.IsNullOrWhiteSpace(line))
                                    {
                                        continue;
                                    }

                                    try
                                    {
                                        logs.Add(JsonHelper.JsonToObject<TLog>(line));
                                    }
                                    catch
                                    {
                                        // ignore partial/corrupted line (e.g. from crash during write)
                                    }
                                }
                            }

                            if (logs.Count != 0)
                            {
                                var ids = logs.Select(x => x.Id).ToList();
                                var existingIds = await db.TLog.Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToHashSetAsync(stoppingToken);

                                var newLogs = logs.Where(x => !existingIds.Contains(x.Id)).ToList();
                                if (newLogs.Count != 0)
                                {
                                    db.TLog.AddRange(newLogs);
                                    await db.SaveChangesAsync(stoppingToken);
                                }
                            }

                            File.Delete(logPath);

                        }
                    }
                }
            }
            catch
            {
            }

            await Task.Delay(1000 * 5, stoppingToken);

        }
    }


    /// <summary>
    /// 恢复上一轮中断时残留的.tmp文件
    /// </summary>
    /// <param name="basePath"></param>
    private static void RecoverTmpFiles(string basePath)
    {
        foreach (var tmpPath in Directory.EnumerateFiles(basePath, "batch-*.log.tmp"))
        {
            try
            {
                var lastWriteUtc = File.GetLastWriteTimeUtc(tmpPath);
                if (DateTime.UtcNow - lastWriteUtc < TmpRecoverAge)
                {
                    continue;
                }

                // 尝试独占打开该文件，如果其他进程或线程还在写入则此处会异常
                using (new FileStream(tmpPath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                }

                var finalPath = tmpPath[..^4]; // remove ".tmp"
                if (File.Exists(finalPath))
                {
                    File.Delete(tmpPath);
                }
                else
                {
                    File.Move(tmpPath, finalPath);
                }
            }
            catch
            {
            }
        }
    }

}
