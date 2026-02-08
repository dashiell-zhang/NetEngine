namespace LLM;

/// <summary>
/// 对话候选结果
/// </summary>
/// <param name="Index">候选序号</param>
/// <param name="Message">候选消息（通常为 assistant）</param>
/// <param name="FinishReason">可选：结束原因（例如 stop/length/tool_calls 等，依供应商而定）</param>
public sealed record ChatChoice(
    int Index,
    ChatMessage Message,
    string? FinishReason = null
);
