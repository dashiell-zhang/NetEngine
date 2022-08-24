using Common;
using System.Reflection;

namespace TaskService.Libraries
{
    public class CronSchedule
    {


        public static async void Builder(CancellationToken stoppingToken, string cronExpression, Action action)
        {
            var nextTime = DateTime.Parse(CronHelper.GetNextOccurrence(cronExpression).ToString("yyyy-MM-dd HH:mm:ss"));

            while (!stoppingToken.IsCancellationRequested)
            {

                var nowTime = DateTime.Parse(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

                if (nextTime == nowTime)
                {
                    _ = Task.Run(() =>
                    {
                        action();
                    });

                    nextTime = DateTime.Parse(CronHelper.GetNextOccurrence(cronExpression).ToString("yyyy-MM-dd HH:mm:ss"));
                }
                else if (nextTime < nowTime)
                {
                    nextTime = DateTime.Parse(CronHelper.GetNextOccurrence(cronExpression).ToString("yyyy-MM-dd HH:mm:ss"));
                }


                await Task.Delay(1000, stoppingToken);
            }
        }



        public static void BatchBuilder(CancellationToken stoppingToken, object context)
        {
            var taskList = context.GetType().GetMethods().Where(t => t.GetCustomAttributes(typeof(CronScheduleAttribute), false).Length > 0).ToList();

            foreach (var t in taskList)
            {
                string cron = t.CustomAttributes.Where(t => t.AttributeType == typeof(CronScheduleAttribute)).FirstOrDefault()!.NamedArguments.Where(t => t.MemberName == "Cron" && t.TypedValue.Value != null).Select(t => t.TypedValue.Value!.ToString()).FirstOrDefault()!;

                Builder(stoppingToken, cron, t, context);
            }
        }



        private static async void Builder(CancellationToken stoppingToken, string cronExpression, MethodInfo action, object context)
        {
            var nextTime = DateTime.Parse(CronHelper.GetNextOccurrence(cronExpression).ToString("yyyy-MM-dd HH:mm:ss"));

            while (!stoppingToken.IsCancellationRequested)
            {
                var nowTime = DateTime.Parse(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

                if (nextTime == nowTime)
                {
                    _ = Task.Run(() =>
                    {
                        action.Invoke(context, null);

                    });

                    nextTime = DateTime.Parse(CronHelper.GetNextOccurrence(cronExpression).ToString("yyyy-MM-dd HH:mm:ss"));
                }
                else if (nextTime < nowTime)
                {
                    nextTime = DateTime.Parse(CronHelper.GetNextOccurrence(cronExpression).ToString("yyyy-MM-dd HH:mm:ss"));
                }

                await Task.Delay(1000, stoppingToken);
            }
        }

    }


    [AttributeUsage(AttributeTargets.Method)]
    public class CronScheduleAttribute : Attribute
    {
        public string Cron { get; set; }

    }
}
