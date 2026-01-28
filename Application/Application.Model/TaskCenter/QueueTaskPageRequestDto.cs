using Application.Model.Shared;

namespace Application.Model.TaskCenter;

/// <summary>
/// 队列任务分页请求入参
/// </summary>
public class QueueTaskPageRequestDto : PageRequestDto
{

    /// <summary>
    /// 是否成功（根据 SuccessTime 是否有值判定）
    /// null：全部；true：仅成功；false：仅未成功
    /// </summary>
    public bool? IsSuccess { get; set; }

    /// <summary>
    /// 创建时间开始
    /// </summary>
    public DateTimeOffset? StartCreateTime { get; set; }

    /// <summary>
    /// 创建时间结束
    /// </summary>
    public DateTimeOffset? EndCreateTime { get; set; }

    /// <summary>
    /// 参数模糊检索
    /// </summary>
    public string? ParameterKeyword { get; set; }
}
