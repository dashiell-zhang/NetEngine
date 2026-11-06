using QueueTask.Database.Models;

namespace QueueTask.Database;

/// <summary>
/// 向单个 Worker 提供其选定业务库的持久化操作
/// </summary>
public interface IQueueTaskStore
{

    /// <summary>
    /// 查询当前可领取的任务标识
    /// </summary>
    Task<List<long>> GetCandidateTaskIdsAsync(string taskName, int takeSize, List<long> runningTaskIdList, CancellationToken cancellationToken);


    /// <summary>
    /// 通过并发控制领取任务
    /// </summary>
    Task<bool> TryClaimTaskAsync(long queueTaskId, string taskName, string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken);


    /// <summary>
    /// 释放尚未执行的领取记录
    /// </summary>
    Task ReleaseClaimAsync(long queueTaskId, string workerId, CancellationToken cancellationToken = default);


    /// <summary>
    /// 记录开始执行及次数并返回任务参数
    /// </summary>
    Task<Models.QueueTask?> StartExecutionAsync(long queueTaskId, string workerId, CancellationToken cancellationToken = default);


    /// <summary>
    /// 更新当前执行者拥有的数据库租约
    /// </summary>
    Task<bool> RenewLeaseAsync(long queueTaskId, string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken);


    /// <summary>
    /// 完成任务并在同库提交父子状态和回调
    /// </summary>
    Task CompleteTaskAsync(long queueTaskId, string workerId, bool isReturnVoid, object? returnObject, CancellationToken cancellationToken = default);


    /// <summary>
    /// 将当前执行者的失败任务恢复为可重试或失败状态
    /// </summary>
    Task MarkTaskFailedAsync(long queueTaskId, string workerId, CancellationToken cancellationToken = default);


    /// <summary>
    /// 获取所属业务库的任务配置
    /// </summary>
    Task<List<TaskSetting>> GetSettingsAsync(CancellationToken cancellationToken = default);


    /// <summary>
    /// 仅补充缺少的任务定义，不覆盖管理端保存的配置
    /// </summary>
    Task EnsureSettingsAsync(IReadOnlyCollection<TaskSetting> definitions, CancellationToken cancellationToken = default);

}
