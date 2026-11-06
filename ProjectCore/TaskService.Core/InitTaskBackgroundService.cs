using global::QueueTask.Database;
using global::QueueTask.Database.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TaskService.Core.QueueTask;
using TaskService.Core.ScheduleTask;

namespace TaskService.Core;
/// <summary>
/// 从所选模块加载任务配置并且只发现明确注册的任务类型
/// </summary>
internal class InitTaskBackgroundService(IQueueTaskStore store, TaskWorkerSelection selection) : BackgroundService
{
    private const string ArgsDefaultParameter = "__args_default__";

    /// <summary>
    /// 建立本任务进程的队列及定时任务注册表
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = await store.GetSettingsAsync(stoppingToken);
        var argScheduleTaskList = settings.Where(item => item.Category == "ScheduleTask" && item.Parameter != null && item.Parameter != ArgsDefaultParameter).ToList();
        foreach (var taskType in selection.TaskTypes)
        {
            ScheduleTaskBuilder.Builder(taskType, argScheduleTaskList);
            QueueTaskBuilder.Builder(taskType);
        }

        await Task.CompletedTask;
    }

}
