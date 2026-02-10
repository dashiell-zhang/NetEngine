using Application.Model.Shared;

namespace Application.Model.LLM.LlmConversation;

/// <summary>
/// LLM 调用日志分页请求
/// </summary>
public class LlmConversationPageRequestDto : PageRequestDto
{

    /// <summary>
    /// 内容关键字（应用Code/名称/System/User/Assistant 模糊检索）
    /// </summary>
    public string? Keyword { get; set; }


    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTimeOffset? StartTime { get; set; }


    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }
}
