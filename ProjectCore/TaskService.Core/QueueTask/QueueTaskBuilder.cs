using System.Reflection;
using System.Runtime.CompilerServices;

namespace TaskService.Core.QueueTask;
public class QueueTaskBuilder
{
    public static readonly Dictionary<string, QueueTaskInfo> queueMethodList = [];


    public static void Builder(Type cls)
    {
        var taskList = cls.GetMethods().Where(t => t.GetCustomAttributes(typeof(QueueTaskAttribute), false).Length > 0).ToList();

        foreach (var method in taskList)
        {

            bool isAsyncVoid = method.ReturnType == typeof(void) && method.GetCustomAttribute<AsyncStateMachineAttribute>() != null;

            if (isAsyncVoid)
            {
                throw new Exception($"{method.Name}返回类型暂不支持 async void 的返回类型方法，请调整为 async Task 或 async Task<>");
            }


            string name = method.CustomAttributes.Where(t => t.AttributeType == typeof(QueueTaskAttribute)).First().NamedArguments.Where(t => t.MemberName == "Name" && t.TypedValue.Value != null).Select(t => t.TypedValue.Value!.ToString()).First()!;

            int semaphore = 1;

            var semaphoreStr = method.CustomAttributes.Where(t => t.AttributeType == typeof(QueueTaskAttribute)).First().NamedArguments.Where(t => t.MemberName == "Semaphore" && t.TypedValue.Value != null).Select(t => t.TypedValue.Value!.ToString()).FirstOrDefault();

            if (semaphoreStr != null)
            {
                semaphore = int.Parse(semaphoreStr);
            }

            int duration = 5;

            var durationStr = method.CustomAttributes.Where(t => t.AttributeType == typeof(QueueTaskAttribute)).First().NamedArguments.Where(t => t.MemberName == "Duration" && t.TypedValue.Value != null).Select(t => t.TypedValue.Value!.ToString()).FirstOrDefault();

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
