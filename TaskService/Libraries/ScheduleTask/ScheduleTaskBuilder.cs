using System.Reflection;

namespace TaskService.Libraries.ScheduleTask
{
    public class ScheduleTaskBuilder
    {

        public static List<ScheduleInfo> scheduleMethodList = new();


        public static void Builder(object context)
        {
            var taskList = context.GetType().GetMethods().Where(t => t.GetCustomAttributes(typeof(ScheduleTaskAttribute), false).Length > 0).ToList();

            foreach (var method in taskList)
            {
                string cron = method.CustomAttributes.Where(t => t.AttributeType == typeof(ScheduleTaskAttribute)).FirstOrDefault()!.NamedArguments.Where(t => t.MemberName == "Cron" && t.TypedValue.Value != null).Select(t => t.TypedValue.Value!.ToString()).FirstOrDefault()!;

                scheduleMethodList.Add(new ScheduleInfo
                {
                    Cron = cron,
                    Method = method,
                    Context = context
                });
            }
        }


        public class ScheduleInfo
        {
            public string Cron { get; set; }

            public MethodInfo Method { get; set; }

            public object Context { get; set; }

            public DateTimeOffset? LastTime { get; set; }
        }
    }

}
