using global::QueueTask.Database;
using global::QueueTask.Database.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskService.Core.QueueTask;
using TaskService.Core.ScheduleTask;

namespace TaskService.Core;

/// <summary>
/// 同步所选模块的任务定义和运行配置，Debug 仍由宿主交互启用
/// </summary>
internal class TaskSettingSyncBackgroundService(IServiceProvider serviceProvider, ILogger<TaskSettingSyncBackgroundService> logger, IQueueTaskStore store) : BackgroundService
{
    private const string ArgsDefaultParameter = "__args_default__";
    private readonly ILogger logger = logger;

    /// <summary>
    /// 初始化完成后按固定周期同步，包括只有带参定义的模块
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        bool isDebug = false;

#if DEBUG
        isDebug = true;
#endif

        var initTaskBackgroundService = serviceProvider.GetServices<IHostedService>().OfType<InitTaskBackgroundService>().First();

        if (!await initTaskBackgroundService.TryWaitForInitializationAsync(logger, stoppingToken))
        {
            return;
        }

        if ((QueueTaskBuilder.queueMethodList.Count != 0 || ScheduleTaskBuilder.scheduleMethodList.Count != 0 || ScheduleTaskBuilder.argsScheduleMethodList.Count != 0) && isDebug == false)
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

    /// <summary>
    /// 读取配置更新运行实例，并在并发保护下补充缺少的定义
    /// </summary>
    private async Task SyncTaskSetting()
    {
        var taskSettings = await store.GetSettingsAsync();
        List<TaskSetting> missingSettings = [];

        var queueTaskSettings = taskSettings.Where(t => t.Category == "QueueTask" && QueueTaskBuilder.queueMethodList.ContainsKey(t.Name)).ToList();

        var scheduleTaskSettings = taskSettings.Where(t => t.Category == "ScheduleTask" && (ScheduleTaskBuilder.scheduleMethodList.ContainsKey(t.Name) || ScheduleTaskBuilder.argsScheduleMethodList.ContainsKey(t.Name))).ToList();


        foreach (var item in QueueTaskBuilder.queueMethodList)
        {
            var task = queueTaskSettings.FirstOrDefault(t => t.Name == item.Key);
            if (task != null)
            {
                item.Value.Semaphore = task.Semaphore ?? item.Value.DefaultSemaphore;
                item.Value.IsEnable = task.IsEnable;
            }
            else
            {
                missingSettings.Add(new TaskSetting
                {
                    Category = "QueueTask",
                    Name = item.Key,
                    Semaphore = item.Value.DefaultSemaphore
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
                    item.Value.Cron = string.IsNullOrWhiteSpace(task.Cron) ? item.Value.DefaultCron : task.Cron;
                    item.Value.IsEnable = task.IsEnable;
                }
                else
                {
                    missingSettings.Add(new TaskSetting
                    {
                        Category = "ScheduleTask",
                        Name = item.Key,
                        Cron = item.Value.DefaultCron
                    });
                }
            }
            else
            {
                var task = scheduleTaskSettings.FirstOrDefault(t => t.Name + ":" + t.Id == item.Key);

                if (task != null)
                {
                    item.Value.Parameter = task.Parameter;

                    item.Value.Cron = string.IsNullOrWhiteSpace(task.Cron) ? item.Value.DefaultCron : task.Cron;
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
            var isHave = taskSettings
                .Where(t => t.Category == "ScheduleTask" && t.Name == item.Key && t.Parameter == ArgsDefaultParameter)
                .Any();

            if (!isHave)
            {
                missingSettings.Add(new TaskSetting
                {
                    Category = "ScheduleTask",
                    Name = item.Key,
                    Cron = item.Value.DefaultCron,
                    Parameter = ArgsDefaultParameter
                });
            }
        }


        var enableArgsTaskList = scheduleTaskSettings
            .Where(t => t.Parameter != null && t.Parameter != ArgsDefaultParameter && t.IsEnable == true)
            .ToList();

        foreach (var item in enableArgsTaskList)
        {
            var scheduleTaskInfo = ScheduleTaskBuilder.argsScheduleMethodList.Where(t => t.Key == item.Name).Select(t => t.Value).FirstOrDefault();

            if (scheduleTaskInfo != null)
            {
                var taskName = item.Name + ":" + item.Id;

                if (!ScheduleTaskBuilder.scheduleMethodList.ContainsKey(taskName))
                {
                    ScheduleTaskBuilder.scheduleMethodList.TryAdd(taskName, new ScheduleTaskInfo
                    {
                        Name = taskName,
                        Cron = string.IsNullOrWhiteSpace(item.Cron) ? scheduleTaskInfo.DefaultCron : item.Cron,
                        DefaultCron = scheduleTaskInfo.DefaultCron,
                        Method = scheduleTaskInfo.Method,
                        Parameter = item.Parameter,
                        IsEnable = item.IsEnable,
                        SkipIfRunning = scheduleTaskInfo.SkipIfRunning,
                    });
                }


            }
        }

        await store.EnsureSettingsAsync(missingSettings);
    }
}
