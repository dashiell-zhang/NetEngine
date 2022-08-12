using Common;
using DistributedLock;
using Repository.Database;

namespace TaskService.Tasks
{
    public class DemoTask : BackgroundService
    {

        private readonly IServiceProvider serviceProvider;

        public DemoTask(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using Timer timer = new(Run, null, 0, 1000);
            await Task.Delay(-1, stoppingToken);
        }



        private void Run(object? state)
        {
            var idHelper = serviceProvider.GetRequiredService<IDHelper>();
            var logger = serviceProvider.GetRequiredService<ILogger<DemoTask>>();

            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var distLock = scope.ServiceProvider.GetRequiredService<IDistributedLock>();

            logger.LogInformation("HelloWord{Id}", idHelper.GetId());

        }


    }
}
