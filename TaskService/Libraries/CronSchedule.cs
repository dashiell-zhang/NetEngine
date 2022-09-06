using Common;
using System.Reflection;

namespace TaskService.Libraries
{
    public class CronSchedule
    {
        private static List<ScheduleInfo> scheduleList = new();
        private static Timer mainTimer;

        public static void Builder(object context)
        {
            var taskList = context.GetType().GetMethods().Where(t => t.GetCustomAttributes(typeof(CronScheduleAttribute), false).Length > 0).ToList();

            foreach (var action in taskList)
            {
                string cron = action.CustomAttributes.Where(t => t.AttributeType == typeof(CronScheduleAttribute)).FirstOrDefault()!.NamedArguments.Where(t => t.MemberName == "Cron" && t.TypedValue.Value != null).Select(t => t.TypedValue.Value!.ToString()).FirstOrDefault()!;

                scheduleList.Add(new ScheduleInfo
                {
                    CronExpression = cron,
                    Action = action,
                    Context = context
                });
            }

            if (mainTimer == default)
            {
                mainTimer = new(Run, null, 0, 1000);
            }
        }


        private static void Run(object? state)
        {
            var nowTime = DateTime.Parse(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

            foreach (var item in scheduleList)
            {
                if (item.LastTime != null)
                {
                    var nextTime = DateTime.Parse(CronHelper.GetNextOccurrence(item.CronExpression, item.LastTime.Value).ToString("yyyy-MM-dd HH:mm:ss"));

                    if (nextTime == nowTime)
                    {
                        item.LastTime = DateTimeOffset.Now;

                        _ = Task.Run(() =>
                        {
                            item.Action.Invoke(item.Context, null);
                        });
                    }
                }
                else
                {
                    item.LastTime = DateTimeOffset.Now.AddSeconds(5);
                }
            }
        }


        private class ScheduleInfo
        {
            public string CronExpression { get; set; }

            public MethodInfo Action { get; set; }

            public object Context { get; set; }

            public DateTimeOffset? LastTime { get; set; }
        }
    }

}
