using Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;


namespace TaskService.Core.ScheduleTask;

/// <summary>
/// 执行所选模块的定时任务并等待动态加入的带参实例
/// </summary>
internal class ScheduleTaskBackgroundService(IServiceProvider serviceProvider, ILogger<ScheduleTaskBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan ScheduleGraceWindow = TimeSpan.FromMilliseconds(256);
    private static readonly TimeSpan SchedulePollInterval = TimeSpan.FromMilliseconds(128);

    private readonly ConcurrentDictionary<string, string?> runingTaskList = new();

    private readonly ConcurrentDictionary<string, string?> skipIfRunningSet = new();

    private readonly ILogger logger = logger;

    /// <summary>
    /// 按现有 Cron 和进程内防重入规则调度当前模块的有效实例
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        using var scanStopping = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, serviceProvider.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);
        stoppingToken = scanStopping.Token;
        var initTaskBackgroundService = serviceProvider.GetServices<IHostedService>().OfType<InitTaskBackgroundService>().First();

        if (!await initTaskBackgroundService.TryWaitForInitializationAsync(logger, stoppingToken))
        {
            return;
        }

        List<Task> executions = [];
        try
        {
            if (ScheduleTaskBuilder.scheduleMethodList.Count != 0 || ScheduleTaskBuilder.argsScheduleMethodList.Count != 0)
            {
                using PeriodicTimer timer = new(SchedulePollInterval);

                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    executions.RemoveAll(task => task.IsCompletedSuccessfully);
                    try
                    {

                        foreach (var item in ScheduleTaskBuilder.scheduleMethodList.Values.Where(t => t.IsEnable).ToList())
                        {
                            stoppingToken.ThrowIfCancellationRequested();
                            try
                            {
                                var nowTime = DateTimeOffset.UtcNow;

                                if (item.LastTime == null)
                                {
                                    item.LastTime = nowTime.AddSeconds(5);
                                }

                                var nextTime = CronHelper.GetNextOccurrence(item.Cron, item.LastTime.Value);

                                var lateBy = nowTime - nextTime;

                                if (lateBy >= TimeSpan.Zero && lateBy <= ScheduleGraceWindow)
                                {
                                    string key = nextTime.ToUnixTimeMilliseconds() + item.Name;

                                    if (runingTaskList.TryAdd(key, null))
                                    {
                                        if (item.SkipIfRunning && !skipIfRunningSet.TryAdd(item.Name, null))
                                        {
                                            runingTaskList.TryRemove(key, out _);
                                        }
                                        else
                                        {
                                            item.LastTime = nextTime;
                                            executions.Add(RunActionAsync(item, key));
                                        }
                                    }
                                }
                                else if (lateBy > ScheduleGraceWindow)
                                {
                                    item.LastTime = nowTime;
                                }
                            }
                            catch (Exception exception)
                            {
                                // 无效配置只暂停本实例的当前任务，配置同步后可重新尝试
                                item.IsEnable = false;
                                logger.LogError(exception, "定时任务 {TaskName} 调度失败，等待配置同步后重试", item.Name);
                            }
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"ExecuteAsync：{ex.Message}");
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            await Task.WhenAll(executions);
        }

    }


    private readonly MethodInfo jsonCloneObject = typeof(JsonHelper).GetMethod("JsonCloneObject", BindingFlags.Static | BindingFlags.Public)!;

    /// <summary>
    /// 执行任务并异步释放作用域，返回实际完成状态供停机排空等待
    /// </summary>
    private async Task RunActionAsync(ScheduleTaskInfo scheduleTaskInfo, string key)
    {
        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();

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

            if (scheduleTaskInfo.SkipIfRunning)
            {
                skipIfRunningSet.TryRemove(scheduleTaskInfo.Name, out _);
            }
        }

    }
}
