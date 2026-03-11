namespace Repository.Database.Enums;

/// <summary>
/// 队列任务执行状态
/// </summary>
public enum QueueTaskStatus
{
    /// <summary>
    /// 待领取
    /// </summary>
    Pending = 0,

    /// <summary>
    /// 已领取并执行中
    /// </summary>
    Running = 1,

    /// <summary>
    /// 执行成功
    /// </summary>
    Succeeded = 2,

    /// <summary>
    /// 已失败且不再重试
    /// </summary>
    Failed = 3
}
