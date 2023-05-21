using Common;
using Repository.Database;
using static TaskService.Libraries.QueueTask.QueueTaskBuilder;
using static TaskService.Libraries.ScheduleTask.ScheduleTaskBuilder;

namespace TaskService.Libraries
{
    public class TaskSyncBackgroundService : BackgroundService
    {

        private readonly IServiceProvider serviceProvider;
        private readonly ILogger logger;
        private readonly IDHelper idHelper;


        public TaskSyncBackgroundService(IServiceProvider serviceProvider, ILogger<TaskSyncBackgroundService> logger, IDHelper idHelper)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;
            this.idHelper = idHelper;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            if (queueMethodList.Any() && scheduleMethodList.Any())
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        if (queueMethodList.Any())
                        {
                            QueueTask();
                        }

                        if (scheduleMethodList.Any())
                        {
                            SyncScheduleTask();
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"ExecuteAsync：{ex.Message}");
                    }

                    await Task.Delay(60000, stoppingToken);
                }
            }
        }

        private void QueueTask()
        {
            Task.Run(() =>
            {
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

                    foreach (var item in queueMethodList)
                    {
                        var task = db.TTask.Where(t => t.Name == item.Key).FirstOrDefault();

                        if (task == null)
                        {
                            task = new()
                            {
                                Id = idHelper.GetId(),
                                CreateTime = DateTime.UtcNow,
                                IsEnable = true,
                                Category = "QueueTask",
                                Name = item.Key,
                                Semaphore = item.Value.Semaphore
                            };

                            db.Add(task);

                            db.SaveChanges();
                        }
                        else
                        {
                            if (task.Semaphore != null && task.Semaphore != item.Value.Semaphore)
                            {
                                item.Value.Semaphore = task.Semaphore.Value;
                            }
                            item.Value.IsEnable = task.IsEnable;
                        }
                    }

                }
                catch (Exception ex)
                {
                    logger.LogError($"QueueTask：{ex.Message}");
                }
            });
        }



        private void SyncScheduleTask()
        {
            Task.Run(() =>
            {
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

                    foreach (var item in scheduleMethodList)
                    {

                        var taskName = item.Method.Name;

                        var task = db.TTask.Where(t => t.Name == taskName).FirstOrDefault();

                        if (task == null)
                        {
                            task = new()
                            {
                                Id = idHelper.GetId(),
                                CreateTime = DateTime.UtcNow,
                                IsEnable = true,
                                Category = "ScheduleTask",
                                Name = taskName,
                                Cron = item.Cron
                            };

                            db.Add(task);

                            db.SaveChanges();
                        }
                        else
                        {
                            if (task.Cron != null && task.Cron != item.Cron)
                            {
                                item.Cron = task.Cron;
                            }
                            item.IsEnable = task.IsEnable;
                        }
                    }

                }
                catch (Exception ex)
                {
                    logger.LogError($"SyncScheduleTask：{ex.Message}");
                }
            });
        }




    }
}
