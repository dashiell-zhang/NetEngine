namespace Application.Model.LLM.LlmConversation;

/// <summary>
/// LLM 调用日志（会话轮次，一问一答一行）
/// </summary>
public class LlmConversationDto
{

    /// <summary>
    /// 标识ID
    /// </summary>
    public long Id { get; set; }


    /// <summary>
    /// LLM 应用ID
    /// </summary>
    public long LlmAppId { get; set; }


    /// <summary>
    /// LLM 应用 Code
    /// </summary>
    public string LlmAppCode { get; set; }


    /// <summary>
    /// LLM 应用名称
    /// </summary>
    public string LlmAppName { get; set; }


    /// <summary>
    /// 创建人ID
    /// </summary>
    public long? CreateUserId { get; set; }

    /// <summary>
    /// 创建人名称
    /// </summary>
    public string? CreateUserName { get; set; }


    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }


    /// <summary>
    /// System 提示词内容（文本）
    /// </summary>
    public string SystemContent { get; set; }


    /// <summary>
    /// 用户消息内容（文本）
    /// </summary>
    public string UserContent { get; set; }


    /// <summary>
    /// 助手消息内容（文本）
    /// </summary>
    public string AssistantContent { get; set; }
}
