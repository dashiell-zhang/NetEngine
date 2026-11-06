using Microsoft.EntityFrameworkCore;
using QueueTask.Database.Models;

namespace QueueTask.Database;

/// <summary>
/// 提供任务开始、租约及配置同步的持久化操作
/// </summary>
public sealed partial class QueueTaskStore<TContext>
{

    /// <summary>
    /// 确认当前执行者并记录本次执行
    /// </summary>
    public async Task<Models.QueueTask?> StartExecutionAsync(long queueTaskId, string workerId, CancellationToken cancellationToken = default)
    {

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var task = await db.Set<Models.QueueTask>().FirstOrDefaultAsync(task => task.Id == queueTaskId && task.WorkerId == workerId && task.Status == QueueTaskStatus.Running, cancellationToken);
        if (task is null)
        {
            return null;
        }

        task.FirstTime ??= DateTimeOffset.UtcNow;
        task.LastTime = DateTimeOffset.UtcNow;
        task.Count++;
        await db.SaveChangesAsync(cancellationToken);
        return task;

    }


    /// <summary>
    /// 仅续期当前执行者仍然拥有的任务
    /// </summary>
    public async Task<bool> RenewLeaseAsync(long queueTaskId, string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var task = await db.Set<Models.QueueTask>().FirstOrDefaultAsync(task => task.Id == queueTaskId, cancellationToken);
        if (task is null || task.WorkerId != workerId || task.Status != QueueTaskStatus.Running || task.SuccessTime is not null)
        {
            return false;
        }

        task.LeaseExpireTime = DateTimeOffset.UtcNow.Add(leaseDuration);
        await db.SaveChangesAsync(cancellationToken);
        return true;

    }


    /// <summary>
    /// 读取模块内有效的配置快照
    /// </summary>
    public async Task<List<TaskSetting>> GetSettingsAsync(CancellationToken cancellationToken = default)
    {

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Set<TaskSetting>().AsNoTracking().ToListAsync(cancellationToken);

    }


    /// <summary>
    /// 利用有效定义的唯一约束处理多个 Worker 同时初始化
    /// </summary>
    public async Task EnsureSettingsAsync(IReadOnlyCollection<TaskSetting> definitions, CancellationToken cancellationToken = default)
    {

        foreach (var definition in definitions)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var exists = await db.Set<TaskSetting>().AnyAsync(item => item.Category == definition.Category && item.Name == definition.Name &&
                (item.Category == "QueueTask" || item.Parameter == null || item.Parameter == "__args_default__"), cancellationToken);
            if (exists)
            {
                continue;
            }

            definition.Id = idService.GetId();
            db.Set<TaskSetting>().Add(definition);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                await using var verify = await dbFactory.CreateDbContextAsync(cancellationToken);
                if (!await verify.Set<TaskSetting>().AnyAsync(item => item.Category == definition.Category && item.Name == definition.Name &&
                    (item.Category == "QueueTask" || item.Parameter == null || item.Parameter == "__args_default__"), cancellationToken))
                {
                    throw;
                }
            }
        }

    }

}
