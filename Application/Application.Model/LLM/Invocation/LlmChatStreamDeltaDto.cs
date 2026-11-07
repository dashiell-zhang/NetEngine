namespace Application.Model.LLM.Invocation;

/// <summary>
/// LLM 流式消息增量
/// </summary>
/// <param name="Role">可选的消息角色</param>
/// <param name="Content">可选的新增文本片段</param>
public sealed record LlmChatStreamDeltaDto(LlmChatRole? Role = null, string? Content = null);
