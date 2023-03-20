using Logger.DataBase.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Repository.Database;

namespace Logger.DataBase.Tasks
{
    public class LogClearTask : BackgroundService
    {

        private readonly int saveDays;
        private readonly IServiceProvider serviceProvider;



        public LogClearTask(IOptionsMonitor<LoggerSetting> config, IServiceProvider serviceProvider)
        {
            saveDays = config.CurrentValue.SaveDays;
            this.serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (saveDays != -1)
            {

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        using var scope = serviceProvider.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

                        var delTime = DateTime.UtcNow.AddDays(-1 * saveDays);

                        db.TLog.Where(t => t.CreateTime <= delTime).ExecuteDelete();
                    }
                    catch
                    {
                    }

                    await Task.Delay(1000 * 60 * 60 * 24, stoppingToken);
                }
            }
        }

    }
}
