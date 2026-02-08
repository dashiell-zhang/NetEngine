namespace LLM;

/// <summary>
/// 流式增量内容（delta）
/// </summary>
/// <param name="Role">可选：部分实现会在首个 chunk 返回 role</param>
/// <param name="Content">可选：本次新增的文本片段</param>
public sealed record ChatStreamDelta(
    ChatRole? Role = null,
    string? Content = null
);
