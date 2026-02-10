using Microsoft.EntityFrameworkCore;
using Repository.Database.Bases;

namespace Repository.Database;

/// <summary>
/// LLM 会话消息表
/// </summary>
[Index(nameof(TrackKey))]
[Index(nameof(ConversationKey))]
public class LlmConversation : CD_User
{

    /// <summary>
    /// 追踪标识
    /// </summary>
    public long TrackKey { get; set; }


    /// <summary>
    /// 会话Key（同一会话的所有消息使用同一个值；可为空）
    /// </summary>
    public long? ConversationKey { get; set; }


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
