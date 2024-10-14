using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using static TaskService.Core.ScheduleTask.ScheduleTaskBuilder;

namespace TaskService.Core.ScheduleTask
{
    public class ScheduleTaskBackgroundService(IServiceProvider serviceProvider, ILogger<ScheduleTaskBackgroundService> logger) : BackgroundService
    {

        private readonly ConcurrentDictionary<string, string?> runingTaskList = new();

        private readonly ILogger logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
#if DEBUG
            await Task.Delay(5000, stoppingToken);
#else
            await Task.Delay(10000, stoppingToken);
#endif

            if (scheduleMethodList.Count != 0)
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {

                        foreach (var item in scheduleMethodList.Values.Where(t => t.IsEnable).ToList())
                        {
                            var nowTime = DateTimeOffset.Parse(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss zzz"));

                            if (item.LastTime == null)
                            {
                                item.LastTime = nowTime.AddSeconds(5);
                            }

                            var nextTime = DateTimeOffset.Parse(CronHelper.GetNextOccurrence(item.Cron, item.LastTime.Value).ToString("yyyy-MM-dd HH:mm:ss zzz"));

                            if (nextTime < nowTime)
                            {
                                item.LastTime = null;
                            }

                            if (nextTime == nowTime)
                            {
                                string key = nowTime.ToUnixTimeSeconds() + item.Name;

                                if (runingTaskList.TryAdd(key, null))
                                {
                                    item.LastTime = nowTime;
                                    RunAction(item, key);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"ExecuteAsync：{ex.Message}");
                    }

                    await Task.Delay(900, stoppingToken);
                }
            }
        }



        private void RunAction(ScheduleTaskInfo scheduleTaskInfo, string key)
        {
            Task.Run(() =>
            {
                try
                {
                    using var scope = serviceProvider.CreateScope();

                    Type serviceType = scheduleTaskInfo.Method.DeclaringType!;

                    object serviceInstance = scope.ServiceProvider.GetRequiredService(serviceType);

                    scheduleTaskInfo.Method.Invoke(serviceInstance, null);
                }
                catch (Exception ex)
                {
                    logger.LogError($"RunAction-{scheduleTaskInfo.Method.Name};{ex.Message}");
                }
                finally
                {
                    runingTaskList.TryRemove(key, out _);
                }
            });
        }
    }
}
