using Common;
using DistributedLock;
using Repository.Database;
using TaskService.Libraries.QueueTask;
using TaskService.Libraries.ScheduleTask;

namespace TaskService.Tasks
{
    public class DemoTask : BackgroundService
    {

        private readonly IServiceProvider serviceProvider;
        private readonly ILogger logger;
        private readonly IDHelper idHelper;


        public DemoTask(IServiceProvider serviceProvider, ILogger<DemoTask> logger, IDHelper idHelper)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;
            this.idHelper = idHelper;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ScheduleTaskBuilder.Builder(this);
            QueueTaskBuilder.Builder(this);

            await Task.Delay(-1, stoppingToken);
        }



        [ScheduleTask(Cron = "0/1 * * * * ?")]
        public void WriteHello()
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
                var distLock = scope.ServiceProvider.GetRequiredService<IDistributedLock>();

                logger.LogInformation("HelloWord{Id}", idHelper.GetId());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "DemoTask.WriteHello");
            }
        }




        [QueueTask(Action = "ShowName",Semaphore =1)]
        public void ShowName(string name)
        {
            Console.WriteLine(DateTime.Now + "姓名：" + name);
        }


    }
}
