namespace TaskService.Core.QueueTask
{
    public class QueueTaskBuilder
    {
        public static readonly Dictionary<string, QueueTaskInfo> queueMethodList = [];


        public static void Builder(Type cls)
        {
            var taskList = cls.GetMethods().Where(t => t.GetCustomAttributes(typeof(QueueTaskAttribute), false).Length > 0).ToList();

            foreach (var method in taskList)
            {
                string name = method.CustomAttributes.Where(t => t.AttributeType == typeof(QueueTaskAttribute)).FirstOrDefault()!.NamedArguments.Where(t => t.MemberName == "Name" && t.TypedValue.Value != null).Select(t => t.TypedValue.Value!.ToString()).First()!;

                int semaphore = 1;

                var semaphoreStr = method.CustomAttributes.Where(t => t.AttributeType == typeof(QueueTaskAttribute)).FirstOrDefault()!.NamedArguments.Where(t => t.MemberName == "Semaphore" && t.TypedValue.Value != null).Select(t => t.TypedValue.Value!.ToString()).FirstOrDefault();

                if (semaphoreStr != null)
                {
                    semaphore = int.Parse(semaphoreStr);
                }

                int duration = 5;

                var durationStr = method.CustomAttributes.Where(t => t.AttributeType == typeof(QueueTaskAttribute)).FirstOrDefault()!.NamedArguments.Where(t => t.MemberName == "Duration" && t.TypedValue.Value != null).Select(t => t.TypedValue.Value!.ToString()).FirstOrDefault();

                if (durationStr != null)
                {
                    duration = int.Parse(durationStr);
                }

                queueMethodList.Add(name, new()
                {
                    Name = name,
                    Semaphore = semaphore,
                    Method = method,
                    Duration = duration,
                });
            }

        }

    }
}
