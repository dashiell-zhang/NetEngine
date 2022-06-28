using Common;
using DistributedLock;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Repository.Database;
using System;
using System.Threading.Tasks;
using System.Timers;

namespace TaskService.Tasks
{
    public class DemoTask : BackgroundService
    {

        private readonly IServiceProvider serviceProvider;

        public DemoTask(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        protected override Task ExecuteAsync(System.Threading.CancellationToken stoppingToken)
        {
            return Task.Run(() =>
            {
                var timer = new Timer(1000 * 1);
                timer.Elapsed += TimerElapsed;
                timer.Start();
            }, stoppingToken);
        }



        private void TimerElapsed(object? sender, ElapsedEventArgs e)
        {
            Run();
        }


        private void Run()
        {
            var snowflakeHelper = serviceProvider.GetRequiredService<SnowflakeHelper>();
            var logger = serviceProvider.GetRequiredService<ILogger<DemoTask>>();

            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var distLock = scope.ServiceProvider.GetRequiredService<IDistributedLock>();

            logger.LogInformation("HelloWord{Id}", snowflakeHelper.GetId());

        }

    }
}
