namespace Application.Model.LLM.Invocation;

/// <summary>
/// LLM 流式候选增量
/// </summary>
/// <param name="Index">候选序号</param>
/// <param name="Delta">本次增量内容</param>
/// <param name="FinishReason">可选的结束原因</param>
public sealed record LlmChatStreamChoiceDto(int Index, LlmChatStreamDeltaDto Delta, string? FinishReason = null);
