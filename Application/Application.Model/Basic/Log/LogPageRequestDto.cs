using Application.Model.Shared;

namespace Application.Model.Basic.Log;

/// <summary>
/// 日志分页请求
/// </summary>
public class LogPageRequestDto : PageRequestDto
{
    /// <summary>
    /// 项目名称
    /// </summary>
    public string? Project { get; set; }

    /// <summary>
    /// 机器名称
    /// </summary>
    public string? MachineName { get; set; }

    /// <summary>
    /// 日志内容（模糊检索）
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// 日志等级
    /// </summary>
    public string? Level { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }
}
