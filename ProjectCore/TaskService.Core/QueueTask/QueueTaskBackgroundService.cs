using Common;
using DistributedLock;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Repository;
using Repository.Database;
using Repository.Database.Enums;
using System.Collections.Concurrent;
using System.Reflection;
using static TaskService.Core.QueueTask.QueueTaskBuilder;

namespace TaskService.Core.QueueTask;

/// <summary>
/// 队列任务后台执行服务
/// </summary>
public class QueueTaskBackgroundService(IServiceProvider serviceProvider, ILogger<QueueTaskBackgroundService> logger, IDistributedLock distLock, IDbContextFactory<DatabaseContext> dbFactory) : BackgroundService
{
    /// <summary>
    /// 队列任务最大重试次数
    /// </summary>
    private const int MaxRetryCount = 3;

    /// <summary>
    /// 队列任务默认租约时长
    /// </summary>
    private static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(QueueTaskBuilder.DefaultDuration);

    private readonly ILogger logger = logger;

    /// <summary>
    /// 当前实例正在执行的任务列表
    /// </summary>
    private readonly ConcurrentDictionary<long, string> runingTaskList = new();

    /// <summary>
    /// 当前 TaskService 实例标识
    /// </summary>
    private readonly string workerId = CreateWorkerId();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        var initTaskBackgroundService = serviceProvider.GetServices<IHostedService>().OfType<InitTaskBackgroundService>().First();

        if (!await initTaskBackgroundService.TryWaitForInitializationAsync(logger, stoppingToken))
        {
            return;
        }

        if (queueMethodList.Count != 0)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    foreach (var item in queueMethodList.Values.Where(t => t.IsEnable).ToList())
                    {

                        var runingTaskIdList = runingTaskList.Where(t => t.Value == item.Name).Select(t => t.Key).ToList();

                        int taskSize = item.Semaphore - runingTaskIdList.Count;

                        if (taskSize > 0)
                        {
                            var queueTaskIdList = await GetCandidateTaskIdsAsync(item, taskSize * 3, runingTaskIdList, stoppingToken);

                            int startedTaskCount = 0;

                            foreach (var queueTaskId in queueTaskIdList)
                            {
                                bool isClaimed = await TryClaimTaskAsync(queueTaskId, item, stoppingToken);
                                if (isClaimed && runingTaskList.TryAdd(queueTaskId, item.Name))
                                {
                                    _ = RunActionAsync(item, queueTaskId);
                                    startedTaskCount++;
                                    if (startedTaskCount >= taskSize)
                                    {
                                        break;
                                    }
                                }
                                else if (isClaimed)
                                {
                                    await ReleaseClaimAsync(queueTaskId);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError($"ExecuteAsync：{ex.Message}");
                }

                await Task.Delay(1000, stoppingToken);
            }
        }
    }


    private readonly MethodInfo jsonCloneObject = typeof(JsonHelper).GetMethod("JsonCloneObject", BindingFlags.Static | BindingFlags.Public)!;

    /// <summary>
    /// 创建当前实例的唯一标识
    /// </summary>
    private static string CreateWorkerId()
    {
        string worker = $"{Environment.MachineName}-{Environment.ProcessId}";
        return worker[..Math.Min(48, worker.Length)];
    }

    /// <summary>
    /// 加载当前任务类型可供领取的候选任务
    /// </summary>
    private async Task<List<long>> GetCandidateTaskIdsAsync(QueueTaskInfo queueTaskInfo, int takeSize, List<long> runningTaskIdList, CancellationToken cancellationToken)
    {
        var nowTime = DateTimeOffset.UtcNow;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.QueueTask
            .AsNoTracking()
            .Where(t => t.Name == queueTaskInfo.Name
                && t.CreateTime < nowTime.AddSeconds(-1)
                && t.SuccessTime == null
                && (t.PlanTime == null || t.PlanTime <= nowTime)
                && runningTaskIdList.Contains(t.Id) == false
                && t.Count < MaxRetryCount
                && ((t.Status == QueueTaskStatus.Pending && (t.LastTime == null || t.LastTime < nowTime.AddMinutes(-5 * t.Count)))
                    || (t.Status == QueueTaskStatus.Running && t.LeaseExpireTime != null && t.LeaseExpireTime < nowTime)))
            .OrderBy(t => t.Count)
            .ThenBy(t => t.LastTime)
            .ThenBy(t => t.CreateTime)
            .Take(takeSize)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 尝试将任务原子标记为当前实例已领取
    /// </summary>
    private async Task<bool> TryClaimTaskAsync(long queueTaskId, QueueTaskInfo queueTaskInfo, CancellationToken cancellationToken)
    {
        var nowTime = DateTimeOffset.UtcNow;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var queueTask = await db.QueueTask.FirstOrDefaultAsync(t => t.Id == queueTaskId, cancellationToken);
        if (queueTask == null || queueTask.Name != queueTaskInfo.Name || queueTask.SuccessTime != null || queueTask.Count >= MaxRetryCount)
        {
            return false;
        }

        bool canClaim = queueTask.Status == QueueTaskStatus.Pending
            || (queueTask.Status == QueueTaskStatus.Running && queueTask.LeaseExpireTime != null && queueTask.LeaseExpireTime < nowTime);

        if (!canClaim || (queueTask.PlanTime != null && queueTask.PlanTime > nowTime))
        {
            return false;
        }

        queueTask.Status = QueueTaskStatus.Running;
        queueTask.WorkerId = workerId;
        queueTask.LeaseExpireTime = nowTime.Add(DefaultLeaseDuration);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    /// <summary>
    /// 释放当前实例已经领取但尚未执行的任务
    /// </summary>
    private async Task ReleaseClaimAsync(long queueTaskId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var queueTask = await db.QueueTask.FirstOrDefaultAsync(t => t.Id == queueTaskId);
        if (queueTask == null || queueTask.WorkerId != workerId || queueTask.Status != QueueTaskStatus.Running || queueTask.SuccessTime != null)
        {
            return;
        }

        queueTask.Status = queueTask.Count >= MaxRetryCount ? QueueTaskStatus.Failed : QueueTaskStatus.Pending;
        queueTask.LeaseExpireTime = null;
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
        }
    }

    /// <summary>
    /// 执行已经领取成功的队列任务
    /// </summary>
    private async Task RunActionAsync(QueueTaskInfo queueTaskInfo, long queueTaskId)
    {
        CancellationTokenSource? renewLeaseTokenSource = null;
        Task? renewLeaseTask = null;
        object? returnObject = null;
        bool isReturnVoid = false;

        try
        {
            using var scope = serviceProvider.CreateScope();

            var queueTaskService = scope.ServiceProvider.GetRequiredService<QueueTaskService>();

            queueTaskService.CurrentTaskId = queueTaskId;

            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();

            await using var db = await factory.CreateDbContextAsync();

            var idService = scope.ServiceProvider.GetRequiredService<IdService>();

            using var lockActionState = await distLock.TryLockAsync(queueTaskInfo.Name, DefaultLeaseDuration, queueTaskInfo.Semaphore);
            if (lockActionState == null)
            {
                await ReleaseClaimAsync(queueTaskId);
                return;
            }

            var queueTask = await db.QueueTask.FirstOrDefaultAsync(t => t.Id == queueTaskId && t.WorkerId == workerId && t.Status == QueueTaskStatus.Running);
            if (queueTask == null)
            {
                return;
            }

            if (queueTask.FirstTime == null)
            {
                queueTask.FirstTime = DateTimeOffset.UtcNow;
            }

            queueTask.LastTime = DateTimeOffset.UtcNow;

            queueTask.Count++;

            await db.SaveChangesAsync();

            isReturnVoid = queueTaskInfo.Method.ReturnType.FullName == "System.Void";

            var parameterType = queueTaskInfo.Method.GetParameters().FirstOrDefault()?.ParameterType;

            Type serviceType = queueTaskInfo.Method.DeclaringType!;

            object serviceInstance = scope.ServiceProvider.GetRequiredService(serviceType);

            renewLeaseTokenSource = new CancellationTokenSource();
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
                    logger.LogError(queueTaskInfo.Method + "方法要求有参数，但队列任务记录缺少参数");
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

            await CompleteTaskAsync(queueTaskId, isReturnVoid, returnObject, idService);
        }
        catch (Exception ex)
        {
            await MarkTaskFailedAsync(queueTaskId);

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
            runingTaskList.TryRemove(queueTaskId, out _);
        }
    }

    /// <summary>
    /// 以新的上下文完成任务收尾并写入回调任务
    /// </summary>
    private async Task CompleteTaskAsync(long queueTaskId, bool isReturnVoid, object? returnObject, IdService idService)
    {
        for (int retryCount = 0; retryCount < 3; retryCount++)
        {
            await using var db = await dbFactory.CreateDbContextAsync();

            var queueTask = await db.QueueTask.FirstOrDefaultAsync(t => t.Id == queueTaskId && t.WorkerId == workerId && t.Status == QueueTaskStatus.Running);
            if (queueTask == null)
            {
                return;
            }

            queueTask.Status = QueueTaskStatus.Succeeded;
            queueTask.SuccessTime = DateTimeOffset.UtcNow;
            queueTask.LeaseExpireTime = null;

            var isHaveChild = await db.QueueTask.Where(t => t.ParentTaskId == queueTaskId).AnyAsync();

            if (!isHaveChild)
            {
                queueTask.ChildSuccessTime = queueTask.SuccessTime;
            }

            if (queueTask.CallbackName != null)
            {
                if (queueTask.CallbackParameter == null && isReturnVoid == false && returnObject != null)
                {
                    queueTask.CallbackParameter = JsonHelper.ObjectToJson(returnObject);
                }

                if (queueTask.ChildSuccessTime != null)
                {
                    Repository.Database.QueueTask callbackTask = new()
                    {
                        Id = idService.GetId(),
                        Status = QueueTaskStatus.Pending,
                        Name = queueTask.CallbackName,
                        Parameter = queueTask.CallbackParameter
                    };

                    db.QueueTask.Add(callbackTask);
                }
            }

            if (queueTask.ParentTaskId != null && queueTask.ChildSuccessTime != null)
            {
                await UpdateParentState(queueTask.ParentTaskId.Value, queueTask.Id);
            }

            try
            {
                await db.SaveChangesAsync();
                return;
            }
            catch (DbUpdateConcurrencyException)
            {
            }

            async Task UpdateParentState(long parentTaskId, long currentTaskId)
            {
                using (await distLock.LockAsync(parentTaskId.ToString()))
                {
                    var isSameLevelHaveWait = await db.QueueTask.Where(t => t.ParentTaskId == parentTaskId && t.Id != currentTaskId && t.ChildSuccessTime == null).AnyAsync();

                    if (!isSameLevelHaveWait)
                    {
                        var parentTaskInfo = await db.QueueTask.Where(t => t.Id == parentTaskId).FirstAsync();

                        parentTaskInfo.ChildSuccessTime = DateTimeOffset.UtcNow;

                        if (parentTaskInfo.CallbackName != null)
                        {
                            Repository.Database.QueueTask callbackTask = new()
                            {
                                Id = idService.GetId(),
                                Status = QueueTaskStatus.Pending,
                                Name = parentTaskInfo.CallbackName,
                                Parameter = parentTaskInfo.CallbackParameter
                            };

                            db.QueueTask.Add(callbackTask);
                        }

                        if (parentTaskInfo.ParentTaskId != null)
                        {
                            await UpdateParentState(parentTaskInfo.ParentTaskId.Value, parentTaskInfo.Id);
                        }
                    }
                }
            }
        }

        throw new DbUpdateConcurrencyException("Complete queue task failed after retries");
    }

    /// <summary>
    /// 在任务执行期间持续延长数据库租约和并发锁
    /// </summary>
    private async Task RenewLeaseAsync(long queueTaskId, QueueTaskInfo queueTaskInfo, IDisposable lockHandle, CancellationToken cancellationToken)
    {
        var renewInterval = GetLeaseRenewInterval(DefaultLeaseDuration);

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

            try
            {
                if (!await distLock.RenewAsync(lockHandle, DefaultLeaseDuration))
                {
                    logger.LogWarning("Renew distributed lock failed for queue task {QueueTaskId}", queueTaskId);
                    continue;
                }

                await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

                var queueTask = await db.QueueTask.FirstOrDefaultAsync(t => t.Id == queueTaskId, cancellationToken);
                if (queueTask == null || queueTask.WorkerId != workerId || queueTask.Status != QueueTaskStatus.Running || queueTask.SuccessTime != null)
                {
                    return;
                }

                queueTask.LeaseExpireTime = DateTimeOffset.UtcNow.Add(DefaultLeaseDuration);
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
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

    /// <summary>
    /// 将执行异常的任务恢复为待重试或失败状态
    /// </summary>
    private async Task MarkTaskFailedAsync(long queueTaskId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var queueTask = await db.QueueTask.FirstOrDefaultAsync(t => t.Id == queueTaskId);
        if (queueTask == null || queueTask.SuccessTime != null || queueTask.WorkerId != workerId)
        {
            return;
        }

        queueTask.LeaseExpireTime = null;
        queueTask.Status = queueTask.Count >= MaxRetryCount ? QueueTaskStatus.Failed : QueueTaskStatus.Pending;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
        }
    }

}
