namespace LLM;

/// <summary>
/// 流式候选增量
/// </summary>
/// <param name="Index">候选序号</param>
/// <param name="Delta">本次增量内容</param>
/// <param name="FinishReason">可选：结束原因（流末尾可能出现）</param>
public sealed record ChatStreamChoiceDelta(
    int Index,
    ChatStreamDelta Delta,
    string? FinishReason = null
);
