using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Repository;
using TaskService.Core.QueueTask;
using TaskService.Core.ScheduleTask;

namespace TaskService.Core;
public class InitTaskBackgroundService(IServiceProvider serviceProvider) : BackgroundService
{

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        // 查找所有继承自 TaskBase 的具体类
        var taskClasses = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(TaskBase)))
            .ToList();

        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            var argScheduleTaskList = db.TaskSetting.Where(t => t.Category == "ScheduleTask" && t.Parameter != null).ToList();

            foreach (Type cls in taskClasses)
            {
                ScheduleTaskBuilder.Builder(cls, argScheduleTaskList);
                QueueTaskBuilder.Builder(cls);
            }
        }

        await Task.CompletedTask;
    }

}
