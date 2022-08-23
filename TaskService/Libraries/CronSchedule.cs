using Common;

namespace TaskService.Libraries
{
    public class CronSchedule
    {


        public static async void Builder(CancellationToken stoppingToken, string cronExpression, Action action)
        {
            var nextTime = CronHelper.GetNextOccurrence(cronExpression).ToString("yyyy-MM-dd HH:mm:ss");

            while (!stoppingToken.IsCancellationRequested)
            {
                var nowTime = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

                if (nextTime == nowTime)
                {
                    nextTime = CronHelper.GetNextOccurrence(cronExpression).ToString("yyyy-MM-dd HH:mm:ss");

                    action();
                }

                await Task.Delay(1000, stoppingToken);
            }
        }

    }
}
