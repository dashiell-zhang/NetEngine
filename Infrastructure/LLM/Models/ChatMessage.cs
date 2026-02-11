namespace LLM;

/// <summary>
/// 对话消息
/// </summary>
/// <param name="Role">消息角色（system/user/assistant/tool）</param>
/// <param name="Content">消息内容（文本；assistant 进行工具调用时可能为空）</param>
/// <param name="ToolCalls">可选：assistant 发起的工具调用列表</param>
/// <param name="ToolCallId">可选：tool 消息关联的 tool_call_id</param>
/// <param name="Name">可选：兼容旧 function calling / 部分供应商字段</param>
public sealed record ChatMessage(
    ChatRole Role,
    string? Content = null,
    IReadOnlyList<ToolCall>? ToolCalls = null,
    string? ToolCallId = null,
    string? Name = null
);
