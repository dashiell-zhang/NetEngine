using Common;
using Repository.Database;
using static TaskService.Libraries.QueueTask.QueueTaskBuilder;
using static TaskService.Libraries.ScheduleTask.ScheduleTaskBuilder;

namespace TaskService.Libraries
{
    public class TaskSettingSyncBackgroundService : BackgroundService
    {

        private readonly IServiceProvider serviceProvider;
        private readonly ILogger logger;
        private readonly IDHelper idHelper;


        public TaskSettingSyncBackgroundService(IServiceProvider serviceProvider, ILogger<TaskSettingSyncBackgroundService> logger, IDHelper idHelper)
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
                            SyncQueueTaskSetting();
                        }

                        if (scheduleMethodList.Any())
                        {
                            SyncScheduleTaskSetting();
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

        private void SyncQueueTaskSetting()
        {
            Task.Run(() =>
            {
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

                    foreach (var item in queueMethodList)
                    {
                        var task = db.TTaskSetting.Where(t => t.Name == item.Key).FirstOrDefault();

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
                    logger.LogError($"SyncQueueTaskSetting：{ex.Message}");
                }
            });
        }



        private void SyncScheduleTaskSetting()
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

                        var taskSetting = db.TTaskSetting.Where(t => t.Name == taskName).FirstOrDefault();

                        if (taskSetting == null)
                        {
                            taskSetting = new()
                            {
                                Id = idHelper.GetId(),
                                CreateTime = DateTime.UtcNow,
                                IsEnable = true,
                                Category = "ScheduleTask",
                                Name = taskName,
                                Cron = item.Cron
                            };

                            db.Add(taskSetting);

                            db.SaveChanges();
                        }
                        else
                        {
                            if (taskSetting.Cron != null && taskSetting.Cron != item.Cron)
                            {
                                item.Cron = taskSetting.Cron;
                            }
                            item.IsEnable = taskSetting.IsEnable;
                        }
                    }

                }
                catch (Exception ex)
                {
                    logger.LogError($"SyncScheduleTaskSetting：{ex.Message}");
                }
            });
        }




    }
}
