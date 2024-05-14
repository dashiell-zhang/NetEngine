using System.Reflection;
using TaskService.Libraries.QueueTask;
using TaskService.Libraries.ScheduleTask;

namespace TaskService.Libraries
{
    public class InitTaskBackgroundService : BackgroundService
    {

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var taskClasses = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(TaskBase)));

            foreach (Type cls in taskClasses)
            {
                ScheduleTaskBuilder.Builder(cls);
                QueueTaskBuilder.Builder(cls);
            }

            await Task.CompletedTask;
        }

    }
}
