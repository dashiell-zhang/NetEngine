using Common;
using System.Reflection;

namespace TaskService.Libraries
{
    public class QueueTaskBuilder
    {
        public static readonly List<QueueInfo> scheduleList = new();


        public static void Builder(object context)
        {
            var taskList = context.GetType().GetMethods().Where(t => t.GetCustomAttributes(typeof(QueueTaskAttribute), false).Length > 0).ToList();

            foreach (var action in taskList)
            {
                string name = action.CustomAttributes.Where(t => t.AttributeType == typeof(QueueTaskAttribute)).FirstOrDefault()!.NamedArguments.Where(t => t.MemberName == "Action" && t.TypedValue.Value != null).Select(t => t.TypedValue.Value!.ToString()).FirstOrDefault()!;

                scheduleList.Add(new()
                {
                    Name = name,
                    Action = action,
                    Context = context
                });
            }

        }


        public static readonly MethodInfo jsonToParameter = typeof(JsonHelper).GetMethod("JsonToObject", BindingFlags.Static | BindingFlags.Public)!;





        public class QueueInfo
        {

            public string Name { get; set; }

            public MethodInfo Action { get; set; }

            public object Context { get; set; }

            public DateTimeOffset? LastTime { get; set; }
        }
    }
}
