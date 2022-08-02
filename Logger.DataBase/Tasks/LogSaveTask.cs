using Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Repository.Database;
using System.Text;

namespace Logger.DataBase.Tasks
{
    public class LogSaveTask : BackgroundService
    {

        private readonly IServiceProvider serviceProvider;


        public LogSaveTask(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    string basePath = Directory.GetCurrentDirectory().Replace("\\", "/") + "/Logs/";

                    if (Directory.Exists(basePath))
                    {
                        List<string> logPaths = IOHelper.GetFolderAllFiles(basePath).Take(10).ToList();

                        if (logPaths.Count != 0)
                        {
                            using var scope = serviceProvider.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

                            foreach (var logPath in logPaths)
                            {
                                var logStr = File.ReadAllText(logPath, Encoding.UTF8);
                                var log = JsonHelper.JsonToObject<TLog>(logStr);


                                var isHave = db.TLog.Where(t => t.IsDelete == false && t.Id == log.Id).Any();

                                if (!isHave)
                                {
                                    db.TLog.Add(log);
                                    db.SaveChanges();
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
