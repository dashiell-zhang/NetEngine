using Microsoft.Extensions.Hosting;
using TaskService.Core.QueueTask;
using TaskService.Core.ScheduleTask;

namespace TaskService.Core
{
    public class InitTaskBackgroundService : BackgroundService
    {

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            // 查找所有继承自 TaskBase 的具体类
            var taskClasses = assemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(TaskBase)))
                .ToList();

            foreach (Type cls in taskClasses)
            {
                ScheduleTaskBuilder.Builder(cls);
                QueueTaskBuilder.Builder(cls);
            }

            await Task.CompletedTask;
        }

    }
}
