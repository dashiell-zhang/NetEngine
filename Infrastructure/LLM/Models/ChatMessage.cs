namespace LLM;

/// <summary>
/// 对话消息
/// </summary>
/// <param name="Role">消息角色（system/user/assistant/tool）</param>
/// <param name="Content">消息内容（文本）</param>
public sealed record ChatMessage(ChatRole Role, string Content);
