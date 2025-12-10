using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Repository.Database;
using TaskService.Core.QueueTask;
using TaskService.Core.ScheduleTask;

namespace TaskService.Core;
public class TaskSettingSyncBackgroundService(IServiceProvider serviceProvider, ILogger<TaskSettingSyncBackgroundService> logger, IdService idService) : BackgroundService
{
    private readonly ILogger logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        bool isDebug = false;

#if DEBUG
        isDebug = true;
#endif

        var initTaskBackgroundService = serviceProvider.GetServices<IHostedService>().OfType<InitTaskBackgroundService>().First();

        while (true)
        {
            if (initTaskBackgroundService.ExecuteTask!.IsCompletedSuccessfully)
            {
                break;
            }
        }

        if ((QueueTaskBuilder.queueMethodList.Count != 0 || ScheduleTaskBuilder.scheduleMethodList.Count != 0) && isDebug == false)
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

        var queueTaskSettings = taskSettings.Where(t => t.Category == "QueueTask" && QueueTaskBuilder.queueMethodList.ContainsKey(t.Name)).ToList();

        var scheduleTaskSettings = taskSettings.Where(t => t.Category == "ScheduleTask" && (ScheduleTaskBuilder.scheduleMethodList.ContainsKey(t.Name) || ScheduleTaskBuilder.argsScheduleMethodList.ContainsKey(t.Name))).ToList();


        foreach (var item in QueueTaskBuilder.queueMethodList)
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

        foreach (var item in ScheduleTaskBuilder.scheduleMethodList)
        {

            if (item.Key.Contains(":") == false)
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
            else
            {
                var task = scheduleTaskSettings.FirstOrDefault(t => t.Name + ":" + t.Id == item.Key);

                if (task != null)
                {
                    item.Value.Parameter = task.Parameter;

                    if (task.Cron != null && task.Cron != item.Value.Cron)
                    {
                        item.Value.Cron = task.Cron;
                    }
                    item.Value.IsEnable = task.IsEnable;
                }
                else
                {
                    //约等于删掉了
                    item.Value.IsEnable = false;
                }
            }
        }

        //添加带参任务默认值到任务配置表中
        foreach (var item in ScheduleTaskBuilder.argsScheduleMethodList)
        {
            var isHave = await db.TTaskSetting.Where(t => t.Category == "ScheduleTask" && t.Name == item.Key && t.Parameter == null).AnyAsync();

            if (!isHave)
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


        var enableArgsTaskList = scheduleTaskSettings.Where(t => t.Parameter != null && t.IsEnable == true).ToList();

        foreach (var item in enableArgsTaskList)
        {
            var scheduleTaskInfo = ScheduleTaskBuilder.argsScheduleMethodList.Where(t => t.Key == item.Name).Select(t => t.Value).FirstOrDefault();

            if (scheduleTaskInfo != null)
            {
                var taskName = item.Name + ":" + item.Id;

                if (!ScheduleTaskBuilder.scheduleMethodList.ContainsKey(taskName))
                {
                    ScheduleTaskBuilder.scheduleMethodList.Add(taskName, new ScheduleTaskInfo
                    {
                        Name = taskName,
                        Cron = item.Cron ?? scheduleTaskInfo.Cron,
                        Method = scheduleTaskInfo.Method,
                        Parameter = item.Parameter,
                        IsEnable = item.IsEnable,
                    });
                }


            }
        }

        await db.SaveChangesAsync();
    }
}
