namespace Application.Model.LLM.Invocation;

/// <summary>
/// LLM 对话消息
/// </summary>
/// <param name="Role">消息角色</param>
/// <param name="Content">消息文本</param>
public sealed record LlmChatMessageDto(LlmChatRole Role, string Content);
