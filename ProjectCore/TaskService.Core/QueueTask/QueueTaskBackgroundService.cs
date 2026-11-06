using global::QueueTask.Database;
using Common;
using DistributedLock;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using static TaskService.Core.QueueTask.QueueTaskBuilder;

namespace TaskService.Core.QueueTask;

/// <summary>
/// 队列任务后台执行服务
/// </summary>
internal class QueueTaskBackgroundService(IServiceProvider serviceProvider, ILogger<QueueTaskBackgroundService> logger, IDistributedLock distLock, IQueueTaskStore store) : BackgroundService
{
    /// <summary>
    /// 队列任务默认租约时长
    /// </summary>
    private static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(QueueTaskBuilder.DefaultDuration);

    private readonly ILogger logger = logger;

    /// <summary>
    /// 仅在停机期限耗尽或服务释放时取消，正常排空期间保持续租
    /// </summary>
    private readonly CancellationTokenSource executionDeadline = new();

    /// <summary>
    /// 避免宿主重复释放取消源
    /// </summary>
    private int disposed;

    /// <summary>
    /// 当前实例正在执行的任务列表
    /// </summary>
    private readonly ConcurrentDictionary<long, string> runingTaskList = new();

    /// <summary>
    /// 当前 TaskService 实例标识
    /// </summary>
    private readonly string workerId = CreateWorkerId();

    /// <summary>
    /// 仅从选定任务库领取已启用的任务并启动执行
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
            while (queueMethodList.Count != 0 && !stoppingToken.IsCancellationRequested)
            {
                executions.RemoveAll(task => task.IsCompletedSuccessfully);
                try
                {
                    foreach (var item in queueMethodList.Values.Where(t => t.IsEnable).ToList())
                    {
                        var runningIds = runingTaskList.Where(t => t.Value == item.Name).Select(t => t.Key).ToList();
                        int capacity = item.Semaphore - runningIds.Count;
                        if (capacity <= 0)
                        {
                            continue;
                        }

                        var candidates = await store.GetCandidateTaskIdsAsync(item.Name, capacity * 3, runningIds, stoppingToken);
                        int started = 0;
                        foreach (var id in candidates)
                        {
                            stoppingToken.ThrowIfCancellationRequested();
                            if (!await store.TryClaimTaskAsync(id, item.Name, workerId, DefaultLeaseDuration, stoppingToken))
                            {
                                continue;
                            }
                            if (stoppingToken.IsCancellationRequested || !runingTaskList.TryAdd(id, item.Name))
                            {
                                await store.ReleaseClaimAsync(id, workerId, executionDeadline.Token);
                                stoppingToken.ThrowIfCancellationRequested();
                                continue;
                            }

                            executions.Add(RunActionAsync(item, id, executionDeadline.Token));
                            if (++started >= capacity)
                            {
                                break;
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "领取队列任务失败");
                }

                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            // BackgroundService 的停机等待包含已启动执行，超时由 StopAsync 的期限控制
            await Task.WhenAll(executions);
        }

    }


    /// <summary>
    /// 停止领取并等待执行收尾，期限耗尽后停止续租和状态提交
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {

        using var registration = cancellationToken.Register(() => executionDeadline.Cancel());
        await base.StopAsync(cancellationToken);

    }


    /// <summary>
    /// 宿主释放时取消仍未完成的基础设施操作
    /// </summary>
    public override void Dispose()
    {

        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }
        executionDeadline.Cancel();
        base.Dispose();
        executionDeadline.Dispose();

    }


    private readonly MethodInfo jsonCloneObject = typeof(JsonHelper).GetMethod("JsonCloneObject", BindingFlags.Static | BindingFlags.Public)!;

    /// <summary>
    /// 创建当前实例的唯一标识
    /// </summary>
    private static string CreateWorkerId()
    {
        string worker = $"{Guid.NewGuid():N}-{Environment.ProcessId}";
        return worker[..Math.Min(48, worker.Length)];
    }

    /// <summary>
    /// 执行已经领取成功的队列任务
    /// </summary>
    private async Task RunActionAsync(QueueTaskInfo queueTaskInfo, long queueTaskId, CancellationToken cancellationToken)
    {
        CancellationTokenSource? renewLeaseTokenSource = null;
        Task? renewLeaseTask = null;
        IDistributedLockHandle? lockActionState = null;
        object? returnObject = null;
        bool isReturnVoid = false;

        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();

            var queueTaskService = scope.ServiceProvider.GetRequiredService<QueueTaskService>();

            queueTaskService.CurrentTaskId = queueTaskId;

            lockActionState = await distLock.TryLockAsync(queueTaskInfo.Name, DefaultLeaseDuration, queueTaskInfo.Semaphore, cancellationToken);
            if (lockActionState == null)
            {
                await store.ReleaseClaimAsync(queueTaskId, workerId, cancellationToken);
                return;
            }

            var queueTask = await store.StartExecutionAsync(queueTaskId, workerId, cancellationToken);
            if (queueTask is null)
            {
                return;
            }

            isReturnVoid = queueTaskInfo.Method.ReturnType.FullName == "System.Void";

            var parameterType = queueTaskInfo.Method.GetParameters().FirstOrDefault()?.ParameterType;

            Type serviceType = queueTaskInfo.Method.DeclaringType!;

            object serviceInstance = scope.ServiceProvider.GetRequiredService(serviceType);

            cancellationToken.ThrowIfCancellationRequested();
            renewLeaseTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            renewLeaseTask = RenewLeaseAsync(queueTaskId, queueTaskInfo, lockActionState, renewLeaseTokenSource.Token);

            if (parameterType != null)
            {
                if (queueTask.Parameter != null)
                {
                    var parameter = jsonCloneObject.MakeGenericMethod(parameterType).Invoke(null, [queueTask.Parameter])!;

                    returnObject = queueTaskInfo.Method.Invoke(serviceInstance, [parameter]);
                }
                else
                {
                    throw new InvalidOperationException(queueTaskInfo.Method + "方法要求有参数，但队列任务记录缺少参数");
                }
            }
            else
            {
                returnObject = queueTaskInfo.Method.Invoke(serviceInstance, null);
            }

            if (returnObject is Task task)
            {
                await task;

                var resultProperty = task.GetType().GetProperty("Result");
                returnObject = resultProperty?.GetValue(task);

                if (returnObject?.GetType().FullName == "System.Threading.Tasks.VoidTaskResult")
                {
                    returnObject = null;
                }
            }

            await store.CompleteTaskAsync(queueTaskId, workerId, isReturnVoid, returnObject, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 已超出停机期限，保留记录及租约由下一实例恢复
        }
        catch (Exception ex)
        {
            try
            {
                await store.MarkTaskFailedAsync(queueTaskId, workerId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception saveException)
            {
                logger.LogError(saveException, "队列任务 {QueueTaskId} 失败状态保存失败，将由租约恢复", queueTaskId);
            }

            var errorLog = new
            {
                ex?.Source,
                ex?.Message,
                ex?.StackTrace,
                InnerSource = ex?.InnerException?.Source,
                InnerMessage = ex?.InnerException?.Message,
                InnerStackTrace = ex?.InnerException?.StackTrace,
            };

            logger.LogError($"QueueTaskRunAction-{queueTaskInfo.Name};{JsonHelper.ObjectToJson(errorLog)}");
        }
        finally
        {
            await StopLeaseRenewAsync(renewLeaseTokenSource, renewLeaseTask);

            if (lockActionState is not null)
            {
                try
                {
                    await lockActionState.DisposeAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Release distributed lock failed for queue task {QueueTaskId}", queueTaskId);
                }
            }

            runingTaskList.TryRemove(queueTaskId, out _);
        }
    }

    /// <summary>
    /// 在任务执行期间持续延长数据库租约和并发锁
    /// </summary>
    /// <param name="queueTaskId">队列任务编号</param>
    /// <param name="queueTaskInfo">队列任务运行信息</param>
    /// <param name="lockHandle">当前持有的并发锁句柄</param>
    /// <param name="cancellationToken">停止续期的取消令牌</param>
    /// <returns>续期循环任务</returns>
    private async Task RenewLeaseAsync(long queueTaskId, QueueTaskInfo queueTaskInfo, IDistributedLockHandle lockHandle, CancellationToken cancellationToken)
    {
        var renewInterval = GetLeaseRenewInterval(DefaultLeaseDuration);
        var distributedLockLeaseLost = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(renewInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!distributedLockLeaseLost)
            {
                try
                {
                    if (!await distLock.RenewAsync(lockHandle, DefaultLeaseDuration, cancellationToken))
                    {
                        distributedLockLeaseLost = true;
                        logger.LogError("Distributed lock lease lost for queue task {QueueTaskId} Business execution and database lease renewal will continue", queueTaskId);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    distributedLockLeaseLost = true;
                    logger.LogError(ex, "Renew distributed lock failed for queue task {QueueTaskId} Business execution and database lease renewal will continue", queueTaskId);
                }
            }

            try
            {
                if (!await store.RenewLeaseAsync(queueTaskId, workerId, DefaultLeaseDuration, cancellationToken))
                {
                    return;
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                logger.LogError(ex, "Renew database lease concurrency conflict for queue task {QueueTaskId} Business execution will continue and renewal will retry", queueTaskId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Renew lease failed for queue task {QueueTaskId}", queueTaskId);
            }
        }
    }

    /// <summary>
    /// 计算续租间隔
    /// </summary>
    private static TimeSpan GetLeaseRenewInterval(TimeSpan leaseDuration)
    {
        double seconds = leaseDuration.TotalSeconds / 3;
        seconds = Math.Clamp(seconds, 5, 30);
        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// 停止续租任务并等待结束
    /// </summary>
    private static async Task StopLeaseRenewAsync(CancellationTokenSource? renewLeaseTokenSource, Task? renewLeaseTask)
    {
        if (renewLeaseTokenSource == null || renewLeaseTask == null)
        {
            return;
        }

        try
        {
            await renewLeaseTokenSource.CancelAsync();
            await renewLeaseTask;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
        }
        finally
        {
            renewLeaseTokenSource.Dispose();
        }
    }

}
