using Common;
using Logger.LocalFile.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace Logger.LocalFile.Tasks
{
    public class LogClearTask : BackgroundService
    {

        private readonly IServiceProvider serviceProvider;
        private readonly int saveDays;


        private object locker = new();


        public LogClearTask(IServiceProvider serviceProvider, IOptionsMonitor<LoggerSetting> config)
        {
            this.serviceProvider = serviceProvider;
            saveDays = config.CurrentValue.SaveDays;
        }

        protected override Task ExecuteAsync(System.Threading.CancellationToken stoppingToken)
        {
            return Task.Run(() =>
            {
                var timer = new Timer(1000 * 60 * 60 * 24);
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
                List<string> logPaths = IOHelper.GetFolderAllFiles(basePath).ToList();

                var deleteTime = DateTime.UtcNow.AddDays(-1 * saveDays);

                if (logPaths.Count != 0)
                {
                    foreach (var logPath in logPaths)
                    {
                        var fileInfo = new FileInfo(logPath);

                        if (fileInfo.CreationTimeUtc < deleteTime)
                        {
                            File.Delete(logPath);
                        }

                    }

                }
            }

        }

    }
}
