using Common;
using DistributedLock;
using Repository.Database;
using TaskService.Libraries;

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



        [ScheduleTask(Cron = "0/1 * * * * ?")]
        public void QueueTaskRun()
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
                var distLock = scope.ServiceProvider.GetRequiredService<IDistributedLock>();

                foreach (var item in QueueTaskBuilder.scheduleList)
                {
                    try
                    {

                        var queueTask = db.TQueueTask.Where(t => t.Action == item.Name && t.SuccessTime == null && t.Count < 3).FirstOrDefault();

                        if (queueTask != null)
                        {
                            using (var lockHandle = distLock.TryLock(queueTask.Id.ToString()))
                            {
                                if (lockHandle != null)
                                {
                                    queueTask.Count++;

                                    if (queueTask.FirstTime == null)
                                    {
                                        queueTask.FirstTime = DateTime.UtcNow;
                                    }

                                    queueTask.LastTime = DateTime.UtcNow;

                                    try
                                    {
                                        Task.Run(() =>
                                        {
                                            var parameterType = item.Action.GetParameters().FirstOrDefault()?.ParameterType;
                                            if (parameterType != null)
                                            {
                                                if (queueTask.Parameter != null)
                                                {
                                                    string jsonStr = JsonHelper.ObjectToJson(queueTask.Parameter);
                                                    var parameter = QueueTaskBuilder.jsonToParameter.MakeGenericMethod(parameterType).Invoke(null, new object[] { jsonStr })!;

                                                    item.Action.Invoke(item.Context, new object[] { parameter });
                                                }
                                                else
                                                {
                                                    logger.LogError(item.Action + "方法要求有参数，但队列任务记录缺少参数");
                                                }
                                            }
                                            else
                                            {
                                                item.Action.Invoke(item.Context, null)?.ToString();
                                            }
                                        });

                                        queueTask.SuccessTime = DateTime.UtcNow;
                                    }
                                    catch
                                    {
                                    }

                                    db.SaveChanges();

                                }
                            }

                        }
                    }
                    catch
                    {
                    }
                }

            }
            catch
            {
            }

        }



        [QueueTask(Action = "ShowName")]
        public void ShowName(string name)
        {
            Console.WriteLine("姓名：" + name);
        }


    }
}
