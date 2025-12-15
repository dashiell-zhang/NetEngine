using Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;


namespace TaskService.Core.ScheduleTask;

public class ScheduleTaskBackgroundService(IServiceProvider serviceProvider, ILogger<ScheduleTaskBackgroundService> logger) : BackgroundService
{

    private readonly ConcurrentDictionary<string, string?> runingTaskList = new();

    private readonly ILogger logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        var initTaskBackgroundService = serviceProvider.GetServices<IHostedService>().OfType<InitTaskBackgroundService>().First();

        if (!await initTaskBackgroundService.TryWaitForInitializationAsync(logger, stoppingToken))
        {
            return;
        }

        if (ScheduleTaskBuilder.scheduleMethodList.Count != 0)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {

                    foreach (var item in ScheduleTaskBuilder.scheduleMethodList.Values.Where(t => t.IsEnable).ToList())
                    {
                        var nowTime = DateTimeOffset.Parse(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss zzz"));

                        if (item.LastTime == null)
                        {
                            item.LastTime = nowTime.AddSeconds(5);
                        }

                        var nextTime = DateTimeOffset.Parse(CronHelper.GetNextOccurrence(item.Cron, item.LastTime.Value).ToString("yyyy-MM-dd HH:mm:ss zzz"));

                        if (nextTime < nowTime)
                        {
                            item.LastTime = null;
                        }

                        if (nextTime == nowTime)
                        {
                            string key = nowTime.ToUnixTimeSeconds() + item.Name;

                            if (runingTaskList.TryAdd(key, null))
                            {
                                item.LastTime = nowTime;
                                RunAction(item, key);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError($"ExecuteAsync：{ex.Message}");
                }

                await Task.Delay(900, stoppingToken);
            }
        }
    }

    private readonly MethodInfo jsonCloneObject = typeof(JsonHelper).GetMethod("JsonCloneObject", BindingFlags.Static | BindingFlags.Public)!;

    private async void RunAction(ScheduleTaskInfo scheduleTaskInfo, string key)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();

            Type serviceType = scheduleTaskInfo.Method.DeclaringType!;

            object serviceInstance = scope.ServiceProvider.GetRequiredService(serviceType);

            var parameterType = scheduleTaskInfo.Method.GetParameters().FirstOrDefault()?.ParameterType;

            Object? returnObject = null;

            if (parameterType != null)
            {
                if (scheduleTaskInfo.Parameter != null)
                {
                    var parameter = jsonCloneObject.MakeGenericMethod(parameterType).Invoke(null, [scheduleTaskInfo.Parameter])!;

                    returnObject = scheduleTaskInfo.Method.Invoke(serviceInstance, [parameter]);
                }
                else
                {
                    logger.LogError(scheduleTaskInfo.Method + "方法要求有参数，但任务记录缺少参数");
                }
            }
            else
            {
                returnObject = scheduleTaskInfo.Method.Invoke(serviceInstance, null);
            }

            if (returnObject is Task task)
            {
                await task;
            }

        }
        catch (Exception ex)
        {
            var errorLog = new
            {
                ex?.Source,
                ex?.Message,
                ex?.StackTrace,
                InnerSource = ex?.InnerException?.Source,
                InnerMessage = ex?.InnerException?.Message,
                InnerStackTrace = ex?.InnerException?.StackTrace,
            };

            logger.LogError($"ScheduleTaskRunAction-{scheduleTaskInfo.Name};{JsonHelper.ObjectToJson(errorLog)}");
        }
        finally
        {
            runingTaskList.TryRemove(key, out _);
        }

    }
}
