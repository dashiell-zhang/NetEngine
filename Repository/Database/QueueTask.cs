using Repository.Bases;
using Repository.Database.Enums;

namespace Repository.Database;

/// <summary>
/// 队列任务表
/// </summary>
public class QueueTask : CD
{
    /// <summary>
    /// 执行状态
    /// </summary>
    public QueueTaskStatus Status { get; set; } = QueueTaskStatus.Pending;

    /// <summary>
    /// 当前领取任务的实例标识
    /// </summary>
    public string? WorkerId { get; set; }

    /// <summary>
    /// 任务租约到期时间
    /// </summary>
    public DateTimeOffset? LeaseExpireTime { get; set; }

    /// <summary>
    /// 任务名称
    /// </summary>
    public string Name { get; set; }


    /// <summary>
    /// 参数
    /// </summary>
    public string? Parameter { get; set; }


    /// <summary>
    /// 计划执行时间
    /// </summary>
    public DateTimeOffset? PlanTime { set; get; }


    /// <summary>
    /// 执行次数
    /// </summary>
    public int Count { get; set; }


    /// <summary>
    /// 首次执行时间
    /// </summary>
    public DateTimeOffset? FirstTime { get; set; }


    /// <summary>
    /// 最后一次执行时间
    /// </summary>
    public DateTimeOffset? LastTime { get; set; }


    /// <summary>
    /// 成功执行时间
    /// </summary>
    public DateTimeOffset? SuccessTime { get; set; }


    /// <summary>
    /// 回调任务名称
    /// </summary>
    public string? CallbackName { get; set; }


    /// <summary>
    /// 回调参数
    /// </summary>
    public string? CallbackParameter { get; set; }


    /// <summary>
    /// 父级任务Id
    /// </summary>
    public long? ParentTaskId { get; set; }
    public virtual QueueTask ParentTask { get; set; }


    /// <summary>
    /// 子集全部执行成功时间
    /// </summary>
    public DateTimeOffset? ChildSuccessTime { get; set; }

}
