using Common;
using System.Reflection;

namespace QueueTask
{
    public class QueueTaskBuilder
    {
        public static readonly Dictionary<string,QueueInfo> queueActionList = new();


        public static void Builder(object context)
        {
            var taskList = context.GetType().GetMethods().Where(t => t.GetCustomAttributes(typeof(QueueTaskAttribute), false).Length > 0).ToList();

            foreach (var action in taskList)
            {
                string name = action.CustomAttributes.Where(t => t.AttributeType == typeof(QueueTaskAttribute)).FirstOrDefault()!.NamedArguments.Where(t => t.MemberName == "Action" && t.TypedValue.Value != null).Select(t => t.TypedValue.Value!.ToString()).FirstOrDefault()!;

                int semaphore = 1;

                var semaphoreStr = action.CustomAttributes.Where(t => t.AttributeType == typeof(QueueTaskAttribute)).FirstOrDefault()!.NamedArguments.Where(t => t.MemberName == "Semaphore" && t.TypedValue.Value != null).Select(t => t.TypedValue.Value!.ToString()).FirstOrDefault();

                if (semaphoreStr != null)
                {
                    semaphore = int.Parse(semaphoreStr);
                }

                queueActionList.Add(name, new()
                {
                    Name = name,
                    Semaphore = semaphore,
                    Action = action,
                    Context = context
                });
            }
        }


        public static readonly MethodInfo jsonToParameter = typeof(JsonHelper).GetMethod("JsonToObject", BindingFlags.Static | BindingFlags.Public)!;



        public class QueueInfo
        {
            public string Name { get; set; }

            public int Semaphore { get; set; }

            public MethodInfo Action { get; set; }

            public object Context { get; set; }
        }
    }
}
