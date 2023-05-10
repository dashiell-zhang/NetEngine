using System.Reflection;

namespace TaskService.Libraries.QueueTask
{
    public class QueueTaskBuilder
    {
        public static readonly Dictionary<string, QueueInfo> queueMethodList = new();


        public static void Builder(object context)
        {
            var taskList = context.GetType().GetMethods().Where(t => t.GetCustomAttributes(typeof(QueueTaskAttribute), false).Length > 0).ToList();

            foreach (var method in taskList)
            {
                string name = method.CustomAttributes.Where(t => t.AttributeType == typeof(QueueTaskAttribute)).FirstOrDefault()!.NamedArguments.Where(t => t.MemberName == "Name" && t.TypedValue.Value != null).Select(t => t.TypedValue.Value!.ToString()).FirstOrDefault()!;

                int semaphore = 1;

                var semaphoreStr = method.CustomAttributes.Where(t => t.AttributeType == typeof(QueueTaskAttribute)).FirstOrDefault()!.NamedArguments.Where(t => t.MemberName == "Semaphore" && t.TypedValue.Value != null).Select(t => t.TypedValue.Value!.ToString()).FirstOrDefault();

                if (semaphoreStr != null)
                {
                    semaphore = int.Parse(semaphoreStr);
                }

                queueMethodList.Add(name, new()
                {
                    Name = name,
                    Semaphore = semaphore,
                    Method = method,
                    Context = context
                });
            }
        }


        public class QueueInfo
        {
            public string Name { get; set; }

            public int Semaphore { get; set; }

            public MethodInfo Method { get; set; }

            public object Context { get; set; }
        }
    }
}
