namespace Application.Model.LLM.Invocation;

/// <summary>
/// LLM 对话候选结果
/// </summary>
/// <param name="Index">候选序号</param>
/// <param name="Message">候选消息</param>
/// <param name="FinishReason">可选的结束原因</param>
public sealed record LlmChatChoiceDto(int Index, LlmChatMessageDto Message, string? FinishReason = null);
