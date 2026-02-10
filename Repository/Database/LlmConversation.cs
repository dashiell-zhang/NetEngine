using Repository.Database.Bases;

namespace Repository.Database;

/// <summary>
/// LLM 会话记录表
/// </summary>
public class LlmConversation : CD_User
{

    /// <summary>
    /// LLM 应用ID
    /// </summary>
    public long LlmAppId { get; set; }
    public virtual LlmApp LlmApp { get; set; }


    /// <summary>
    /// 系统提示词
    /// </summary>
    public string SystemContent { get; set; }


    /// <summary>
    /// 用户消息
    /// </summary>
    public string UserContent { get; set; }


    /// <summary>
    /// 助手消息
    /// </summary>
    public string AssistantContent { get; set; }

}
