using Common;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using QueueTask.Database.Models;

namespace QueueTask.Database;

/// <summary>
/// 在所选业务库执行任务领取、续租、完成和失败处理
/// </summary>
public sealed partial class QueueTaskStore<TContext>(IDbContextFactory<TContext> dbFactory, IdService idService) : IQueueTaskStore where TContext : DbContext
{

    /// <summary>
    /// 队列最大自动执行次数
    /// </summary>
    private const int MaxRetryCount = 3;


    /// <summary>
    /// 加载当前任务类型可供领取的候选任务
    /// </summary>
    public async Task<List<long>> GetCandidateTaskIdsAsync(string taskName, int takeSize, List<long> runningTaskIdList, CancellationToken cancellationToken)
    {
        var nowTime = DateTimeOffset.UtcNow;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // 最后一次执行中断后不再自动重试，但必须收敛状态，避免永久显示执行中
        await db.Set<Models.QueueTask>()
            .Where(task => task.Name == taskName && task.Status == QueueTaskStatus.Running && task.SuccessTime == null
                && task.Count >= MaxRetryCount && task.LeaseExpireTime != null && task.LeaseExpireTime < nowTime)
            .ExecuteUpdateAsync(setters => setters.SetProperty(task => task.Status, QueueTaskStatus.Failed)
                .SetProperty(task => task.WorkerId, (string?)null)
                .SetProperty(task => task.LeaseExpireTime, (DateTimeOffset?)null), cancellationToken);

        return await db.Set<Models.QueueTask>()
            .AsNoTracking()
            .Where(t => t.Name == taskName
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
    public async Task<bool> TryClaimTaskAsync(long queueTaskId, string taskName, string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        var nowTime = DateTimeOffset.UtcNow;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var queueTask = await db.Set<Models.QueueTask>().FirstOrDefaultAsync(t => t.Id == queueTaskId, cancellationToken);
        if (queueTask == null || queueTask.Name != taskName || queueTask.SuccessTime != null || queueTask.Count >= MaxRetryCount)
        {
            return false;
        }

        bool canClaim = (queueTask.Status == QueueTaskStatus.Pending && (queueTask.LastTime is null || queueTask.LastTime < nowTime.AddMinutes(-5 * queueTask.Count)))
            || (queueTask.Status == QueueTaskStatus.Running && queueTask.LeaseExpireTime != null && queueTask.LeaseExpireTime < nowTime);

        if (!canClaim || (queueTask.PlanTime != null && queueTask.PlanTime > nowTime))
        {
            return false;
        }

        queueTask.Status = QueueTaskStatus.Running;
        queueTask.WorkerId = workerId;
        queueTask.LeaseExpireTime = nowTime.Add(leaseDuration);

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
    public async Task ReleaseClaimAsync(long queueTaskId, string workerId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var queueTask = await db.Set<Models.QueueTask>().FirstOrDefaultAsync(t => t.Id == queueTaskId, cancellationToken);
        if (queueTask == null || queueTask.WorkerId != workerId || queueTask.Status != QueueTaskStatus.Running || queueTask.SuccessTime != null)
        {
            return;
        }

        queueTask.Status = queueTask.Count >= MaxRetryCount ? QueueTaskStatus.Failed : QueueTaskStatus.Pending;
        queueTask.LeaseExpireTime = null;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
        }
    }


    /// <summary>
    /// 以新的上下文完成任务收尾并写入回调任务
    /// </summary>
    public async Task CompleteTaskAsync(long queueTaskId, string workerId, bool isReturnVoid, object? returnObject, CancellationToken cancellationToken = default)
    {

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var task = await LockTaskAsync(db, queueTaskId, cancellationToken);
        if (task is null || task.WorkerId != workerId || task.Status != QueueTaskStatus.Running)
        {
            return;
        }

        task.Status = QueueTaskStatus.Succeeded;
        task.SuccessTime = DateTimeOffset.UtcNow;
        task.LeaseExpireTime = null;
        if (task.CallbackName is not null && task.CallbackParameter is null && !isReturnVoid && returnObject is not null)
        {
            task.CallbackParameter = JsonHelper.ObjectToJson(returnObject);
        }

        await CompleteSubtreeAsync(db, task, null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

    }


    /// <summary>
    /// 自下而上锁定父记录，成功状态和回调入队在同一事务提交
    /// </summary>
    private async Task CompleteSubtreeAsync(TContext db, Models.QueueTask task, long? completedChildId, CancellationToken cancellationToken)
    {

        if (task.Status != QueueTaskStatus.Succeeded || task.SuccessTime is null || task.ChildSuccessTime is not null)
        {
            return;
        }
        var unfinished = await db.Set<Models.QueueTask>().AnyAsync(child => child.ParentTaskId == task.Id && child.Id != completedChildId && child.ChildSuccessTime == null, cancellationToken);
        if (unfinished)
        {
            return;
        }

        task.ChildSuccessTime = DateTimeOffset.UtcNow;
        if (task.CallbackName is not null)
        {
            db.Set<Models.QueueTask>().Add(new Models.QueueTask
            {
                Id = idService.GetId(),
                Status = QueueTaskStatus.Pending,
                Name = task.CallbackName,
                Parameter = task.CallbackParameter
            });
        }

        if (task.ParentTaskId is long parentId)
        {
            var parent = await LockTaskAsync(db, parentId, cancellationToken) ?? throw new InvalidOperationException("任务的父记录不存在");
            await CompleteSubtreeAsync(db, parent, task.Id, cancellationToken);
        }

    }


    /// <summary>
    /// 使用模型实际表名锁定任务并读取 xmin，行锁一直保持到外层事务结束
    /// </summary>
    internal static Task<Models.QueueTask?> LockTaskAsync(TContext db, long id, CancellationToken cancellationToken = default)
    {

        var entity = db.Model.FindEntityType(typeof(Models.QueueTask))!;
        var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
        var helper = db.GetService<ISqlGenerationHelper>();
        var tableName = helper.DelimitIdentifier(table.Name, table.Schema);
        var idColumn = helper.DelimitIdentifier(entity.FindProperty(nameof(Models.QueueTask.Id))!.GetColumnName(table)!);
        // 仅拼接 EF 模型中已引用的标识符，任务编号始终作为数据库参数传入
        var sql = $"SELECT q.*, q.xmin FROM {tableName} AS q WHERE q.{idColumn} = {{0}} FOR UPDATE";
        return db.Set<Models.QueueTask>().FromSqlRaw(sql, id).SingleOrDefaultAsync(cancellationToken);

    }


    /// <summary>
    /// 将执行异常的任务恢复为待重试或失败状态
    /// </summary>
    public async Task MarkTaskFailedAsync(long queueTaskId, string workerId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var queueTask = await db.Set<Models.QueueTask>().FirstOrDefaultAsync(t => t.Id == queueTaskId, cancellationToken);
        if (queueTask == null || queueTask.SuccessTime != null || queueTask.WorkerId != workerId || queueTask.Status != QueueTaskStatus.Running)
        {
            return;
        }

        queueTask.LeaseExpireTime = null;
        queueTask.Status = queueTask.Count >= MaxRetryCount ? QueueTaskStatus.Failed : QueueTaskStatus.Pending;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
        }
    }

}
