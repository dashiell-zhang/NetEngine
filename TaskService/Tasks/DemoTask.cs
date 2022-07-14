using Common;
using DistributedLock;
using Repository.Database;
using System.Timers;
using Timer = System.Timers.Timer;

namespace TaskService.Tasks
{
    public class DemoTask : BackgroundService
    {

        private readonly IServiceProvider serviceProvider;

        public DemoTask(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
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
