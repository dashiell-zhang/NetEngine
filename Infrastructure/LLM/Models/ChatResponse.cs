namespace LLM;

/// <summary>
/// 对话响应（Chat Completion）
/// </summary>
/// <param name="Model">实际使用的模型名称</param>
/// <param name="Choices">候选结果列表</param>
/// <param name="Usage">可选：用量统计</param>
/// <param name="Id">可选：请求/响应 ID（用于追踪）</param>
public sealed record ChatResponse(
    string Model,
    IReadOnlyList<ChatChoice> Choices,
    Usage? Usage = null,
    string? Id = null
);
