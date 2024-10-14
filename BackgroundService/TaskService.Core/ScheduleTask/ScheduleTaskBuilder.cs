namespace TaskService.Core.ScheduleTask
{
    public class ScheduleTaskBuilder
    {

        public static readonly Dictionary<string, ScheduleTaskInfo> scheduleMethodList = [];


        public static void Builder(Type cls)
        {
            var taskList = cls.GetMethods().Where(t => t.GetCustomAttributes(typeof(ScheduleTaskAttribute), false).Length > 0).ToList();

            foreach (var method in taskList)
            {
                string name = method.CustomAttributes.Where(t => t.AttributeType == typeof(ScheduleTaskAttribute)).FirstOrDefault()!.NamedArguments.Where(t => t.MemberName == "Name" && t.TypedValue.Value != null).Select(t => t.TypedValue.Value!.ToString()).FirstOrDefault()!;

                string cron = method.CustomAttributes.Where(t => t.AttributeType == typeof(ScheduleTaskAttribute)).FirstOrDefault()!.NamedArguments.Where(t => t.MemberName == "Cron" && t.TypedValue.Value != null).Select(t => t.TypedValue.Value!.ToString()).FirstOrDefault()!;

                scheduleMethodList.Add(name, new ScheduleTaskInfo
                {
                    Name = name,
                    Cron = cron,
                    Method = method
                });
            }
        }

    }

}
