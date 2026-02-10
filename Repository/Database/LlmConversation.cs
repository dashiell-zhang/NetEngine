using Microsoft.EntityFrameworkCore;
using Repository.Database.Bases;

namespace Repository.Database;

/// <summary>
/// LLM 会话消息表（同一 ConversationId 下为一段对话历史）
/// </summary>
[Index(nameof(ConversationId))]
[Index(nameof(LlmAppId))]
[Index(nameof(ConversationId), nameof(LlmAppId))]
public class LlmConversation : CD_User
{

    /// <summary>
    /// 会话ID（同一会话的所有消息使用同一个值）
    /// </summary>
    public long ConversationId { get; set; }


    /// <summary>
    /// LLM 应用ID
    /// </summary>
    public long LlmAppId { get; set; }
    public virtual LlmApp LlmApp { get; set; }


    /// <summary>
    /// 消息角色（system/user/assistant/tool）
    /// </summary>
    public string Role { get; set; }


    /// <summary>
    /// 消息内容（文本）
    /// </summary>
    public string Content { get; set; }
}

