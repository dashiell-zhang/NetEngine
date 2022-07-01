using Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Repository.Database;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Logger.DataBase.Tasks
{
    public class LogSaveTask : BackgroundService
    {

        private readonly IServiceProvider serviceProvider;


        private object locker = new();


        public LogSaveTask(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        protected override Task ExecuteAsync(System.Threading.CancellationToken stoppingToken)
        {
            return Task.Run(() =>
            {
                var timer = new Timer(1000 * 5);
                timer.Elapsed += TimerElapsed;
                timer.Start();
            }, stoppingToken);
        }



        private void TimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                lock (locker)
                {
                    Run();
                }
            }
            catch
            {

            }
        }


        private void Run()
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

    }
}
