using Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Repository.Database;
using System.Text;

namespace Logger.DataBase.Tasks
{
    internal class LogSaveTask(IServiceProvider serviceProvider) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    string basePath = Path.Combine(Directory.GetCurrentDirectory(), "Logs");

                    if (Directory.Exists(basePath))
                    {
                        List<string> logPaths = [.. IOHelper.GetFolderAllFiles(basePath).Take(10)];

                        if (logPaths.Count != 0)
                        {
                            using var scope = serviceProvider.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

                            foreach (var logPath in logPaths)
                            {
                                var logStr = File.ReadAllText(logPath, Encoding.UTF8);
                                var log = JsonHelper.JsonToObject<TLog>(logStr);


                                var isHave = await db.TLog.Where(t => t.Id == log.Id).AnyAsync(cancellationToken: stoppingToken);

                                if (!isHave)
                                {
                                    db.TLog.Add(log);
                                    await db.SaveChangesAsync(stoppingToken);
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

    }
}
