namespace LLM;

/// <summary>
/// 模型发起的工具调用
/// </summary>
/// <param name="Id">工具调用 ID（tool_call_id）</param>
/// <param name="Name">工具名称</param>
/// <param name="ArgumentsJson">参数 JSON 字符串（原始文本；由工具侧解析）</param>
public sealed record ToolCall(
    string Id,
    string Name,
    string ArgumentsJson
);

