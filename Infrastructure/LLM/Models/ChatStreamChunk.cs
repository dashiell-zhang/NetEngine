namespace LLM;

/// <summary>
/// 对话流式分片（SSE chunk / delta）
/// </summary>
/// <param name="Model">实际使用的模型名称</param>
/// <param name="Choices">增量候选列表</param>
/// <param name="Usage">可选：部分供应商会在流末尾附带用量</param>
/// <param name="Id">可选：请求/响应 ID（用于追踪）</param>
public sealed record ChatStreamChunk(
    string Model,
    IReadOnlyList<ChatStreamChoiceDelta> Choices,
    Usage? Usage = null,
    string? Id = null
);
