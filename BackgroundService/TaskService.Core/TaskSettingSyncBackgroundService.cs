using IdentifierGenerator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Repository.Database;
using static TaskService.Core.QueueTask.QueueTaskBuilder;
using static TaskService.Core.ScheduleTask.ScheduleTaskBuilder;

namespace TaskService.Core
{
    public class TaskSettingSyncBackgroundService(IServiceProvider serviceProvider, ILogger<TaskSettingSyncBackgroundService> logger, IdService idService) : BackgroundService
    {
        private readonly ILogger logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            bool isDebug = false;

#if DEBUG
            isDebug = true;
#endif

            await Task.Delay(5000, stoppingToken);

            if ((queueMethodList.Count != 0 || scheduleMethodList.Count != 0) && isDebug == false)
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        if (queueMethodList.Count != 0)
                        {
                            SyncQueueTaskSetting();
                        }

                        if (scheduleMethodList.Count != 0)
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
                                Id = idService.GetId(),
                                Category = "QueueTask",
                                Name = item.Key,
                                Semaphore = item.Value.Semaphore,
                                Duration = item.Value.Duration
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

                            if (task.Duration != null && task.Duration != item.Value.Duration)
                            {
                                item.Value.Duration = task.Duration.Value;
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

                        var taskSetting = db.TTaskSetting.Where(t => t.Name == item.Key).FirstOrDefault();

                        if (taskSetting == null)
                        {
                            taskSetting = new()
                            {
                                Id = idService.GetId(),
                                Category = "ScheduleTask",
                                Name = item.Key,
                                Cron = item.Value.Cron
                            };

                            db.Add(taskSetting);

                            db.SaveChanges();
                        }
                        else
                        {
                            if (taskSetting.Cron != null && taskSetting.Cron != item.Value.Cron)
                            {
                                item.Value.Cron = taskSetting.Cron;
                            }
                            item.Value.IsEnable = taskSetting.IsEnable;
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
