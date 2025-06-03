using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
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
                        await SyncTaskSetting();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"ExecuteAsync：{ex.Message}");
                    }

                    await Task.Delay(60000, stoppingToken);
                }
            }
        }

        private async Task SyncTaskSetting()
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            var taskSettings = await db.TTaskSetting.ToListAsync();

            var queueTaskSettings = taskSettings.Where(t => t.Category == "QueueTask" && queueMethodList.ContainsKey(t.Name)).ToList();

            var scheduleTaskSettings = taskSettings.Where(t => t.Category == "ScheduleTask" && scheduleMethodList.ContainsKey(t.Name)).ToList();


            foreach (var item in queueMethodList)
            {
                var task = queueTaskSettings.FirstOrDefault(t => t.Name == item.Key);
                if (task != null)
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
                else
                {
                    db.TTaskSetting.Add(new TTaskSetting
                    {
                        Id = idService.GetId(),
                        Category = "QueueTask",
                        Name = item.Key,
                        Semaphore = item.Value.Semaphore,
                        Duration = item.Value.Duration
                    });
                }
            }

            foreach (var item in scheduleMethodList)
            {
                var task = scheduleTaskSettings.FirstOrDefault(t => t.Name == item.Key);
                if (task != null)
                {
                    if (task.Cron != null && task.Cron != item.Value.Cron)
                    {
                        item.Value.Cron = task.Cron;
                    }
                    item.Value.IsEnable = task.IsEnable;
                }
                else
                {
                    db.TTaskSetting.Add(new TTaskSetting
                    {
                        Id = idService.GetId(),
                        Category = "ScheduleTask",
                        Name = item.Key,
                        Cron = item.Value.Cron
                    });
                }
            }

            await db.SaveChangesAsync();
        }
    }
}
