using System.Reflection;
using System.Runtime.CompilerServices;

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
                bool isAsyncVoid = method.ReturnType == typeof(void) && method.GetCustomAttribute<AsyncStateMachineAttribute>() != null;

                if (isAsyncVoid)
                {
                    throw new Exception($"{method.Name}返回类型暂不支持 async void 的返回类型方法，请调整为 async Task");
                }

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
