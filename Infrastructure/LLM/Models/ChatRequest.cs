namespace LLM;

using System.Text.Json.Nodes;

/// <summary>
/// 对话请求（Chat Completion）
/// </summary>
/// <param name="Model">模型名称（优先使用该值；可由 Provider 侧提供默认值）</param>
/// <param name="Messages">消息列表（按时间顺序）</param>
/// <param name="User">可选：终端用户标识（用于审计/限流等）</param>
/// <param name="ExtraBody">可选：额外请求参数（直接透传到 OpenAI-compatible body 根字段）</param>
public sealed record ChatRequest(
    string Model,
    IReadOnlyList<ChatMessage> Messages,
    string? User = null,
    Dictionary<string, JsonNode>? ExtraBody = null
);
