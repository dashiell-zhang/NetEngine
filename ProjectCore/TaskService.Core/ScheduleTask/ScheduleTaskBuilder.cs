using global::QueueTask.Database.Models;
using Common;
using System.Reflection;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace TaskService.Core.ScheduleTask;
/// <summary>
/// 维护选定任务模块的定时任务定义和可动态增加的运行实例
/// </summary>
public class ScheduleTaskBuilder
{

    /// <summary>
    /// 当前模块的运行实例，支持配置同步与调度并发访问
    /// </summary>
    public static readonly ConcurrentDictionary<string, ScheduleTaskInfo> scheduleMethodList = [];


    public static readonly Dictionary<string, ScheduleTaskInfo> argsScheduleMethodList = [];


    /// <summary>
    /// 从明确选定的类型建立定时定义和已有带参实例
    /// </summary>
    public static void Builder(Type cls, List<TaskSetting> taskSettings)
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

            bool skipIfRunning = (bool?)method.CustomAttributes.Where(t => t.AttributeType == typeof(ScheduleTaskAttribute)).FirstOrDefault()!.NamedArguments.Where(t => t.MemberName == "SkipIfRunning").Select(t => t.TypedValue.Value).FirstOrDefault() ?? false;

            CronHelper.Validate(cron);
            var parameterType = method.GetParameters().FirstOrDefault()?.ParameterType;

            if (parameterType == null)
            {
                AddInstance(name, new ScheduleTaskInfo
                {
                    Name = name,
                    Cron = cron,
                    DefaultCron = cron,
                    Method = method,
                    SkipIfRunning = skipIfRunning
                });
            }
            else
            {

                argsScheduleMethodList.Add(name, new ScheduleTaskInfo
                {
                    Name = name,
                    Cron = cron,
                    DefaultCron = cron,
                    Method = method,
                    SkipIfRunning = skipIfRunning
                });

                var argsTaskList = taskSettings.Where(t => t.Name == name).ToList();

                foreach (var item in argsTaskList)
                {
                    string taskName = name + ":" + item.Id;

                    AddInstance(taskName, new ScheduleTaskInfo
                    {
                        Name = taskName,
                        Cron = string.IsNullOrWhiteSpace(item.Cron) ? cron : item.Cron,
                        DefaultCron = cron,
                        Method = method,
                        Parameter = item.Parameter,
                        SkipIfRunning = skipIfRunning
                    });
                }

            }

        }
    }


    /// <summary>
    /// 初始化时拒绝重复名称，避免静默覆盖已发现的任务
    /// </summary>
    private static void AddInstance(string name, ScheduleTaskInfo info)
    {

        if (!scheduleMethodList.TryAdd(name, info))
        {
            throw new InvalidOperationException($"重复的定时任务名称 {name}");
        }

    }

}

