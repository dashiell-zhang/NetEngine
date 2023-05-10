using DistributedLock;
using System.Collections;
using TaskService.Libraries.ScheduleTask;
using static TaskService.Libraries.ScheduleTask.ScheduleTaskBuilder;

namespace TaskService.Libraries.QueueTask
{
    public class ScheduleTaskBackgroundService : BackgroundService
    {

        private static readonly Hashtable historyList = new();
        private static readonly List<string> historyKeyList = new();



        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            if (scheduleMethodList.Any())
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var nowTime = DateTime.Parse(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

                        foreach (var key in historyKeyList)
                        {
                            var keyTime = DateTime.Parse(key[..19]);

                            if (keyTime! <= nowTime.AddSeconds(-5))
                            {
                                historyList.Remove(key);
                            }
                        }

                        foreach (var item in scheduleMethodList)
                        {
                            if (item.LastTime != null)
                            {
                                var nextTime = DateTime.Parse(CronHelper.GetNextOccurrence(item.Cron, item.LastTime.Value).ToString("yyyy-MM-dd HH:mm:ss"));

                                if (nextTime == nowTime)
                                {
                                    try
                                    {
                                        string key = nextTime.ToString("yyyy-MM-dd HH:mm:ss") + " " + item.Method.DeclaringType?.FullName + "." + item.Method.Name;
                                        historyList.Add(key, null);
                                        historyKeyList.Add(key);

                                        item.LastTime = DateTimeOffset.Now;

                                        RunAction(item);
                                    }
                                    catch
                                    {
                                    }
                                }
                            }
                            else
                            {
                                item.LastTime = DateTimeOffset.Now.AddSeconds(5);
                            }
                        }
                    }
                    catch
                    {
                    }

                    await Task.Delay(900, stoppingToken);
                }
            }
        }



        private void RunAction(ScheduleInfo scheduleInfo)
        {
            try
            {
                Task.Run(() =>
                {
                    scheduleInfo.Method.Invoke(scheduleInfo.Context, null);
                });

            }
            catch
            {
            }
        }
    }
}
