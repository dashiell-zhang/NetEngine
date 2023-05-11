using System.Collections.Concurrent;
using TaskService.Libraries.ScheduleTask;
using static TaskService.Libraries.ScheduleTask.ScheduleTaskBuilder;

namespace TaskService.Libraries.QueueTask
{
    public class ScheduleTaskBackgroundService : BackgroundService
    {

        private readonly ConcurrentDictionary<string, string?> historyList = new();

        private readonly ILogger logger;

        public ScheduleTaskBackgroundService(ILogger<ScheduleTaskBackgroundService> logger)
        {
            this.logger = logger;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            if (scheduleMethodList.Any())
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var nowTime = DateTime.Parse(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

                        foreach (var key in historyList.Keys)
                        {
                            var keyTime = DateTime.Parse(key[..19]);

                            if (keyTime! <= nowTime.AddSeconds(-3))
                            {
                                historyList.TryRemove(key, out _);
                            }
                        }

                        foreach (var item in scheduleMethodList)
                        {
                            if (item.LastTime == null)
                            {
                                item.LastTime = DateTimeOffset.Now.AddSeconds(5);
                            }

                            var nextTime = DateTime.Parse(CronHelper.GetNextOccurrence(item.Cron, item.LastTime.Value).ToString("yyyy-MM-dd HH:mm:ss"));

                            if (nextTime == nowTime)
                            {
                                string key = nextTime.ToString("yyyy-MM-dd HH:mm:ss") + " " + item.Method.DeclaringType?.FullName + "." + item.Method.Name;

                                if (historyList.TryAdd(key, null))
                                {
                                    item.LastTime = DateTimeOffset.Now;

                                    RunAction(item);
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



        private void RunAction(ScheduleInfo scheduleInfo)
        {
            Task.Run(() =>
            {
                try
                {
                    scheduleInfo.Method.Invoke(scheduleInfo.Context, null);
                }
                catch (Exception ex)
                {
                    logger.LogError($"RunAction-{scheduleInfo.Method.Name};{ex.Message}");
                }
            });
        }
    }
}
