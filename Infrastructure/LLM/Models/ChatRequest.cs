namespace LLM;

/// <summary>
/// 对话请求（Chat Completion）
/// </summary>
/// <param name="Model">模型名称（优先使用该值；可由 Provider 侧提供默认值）</param>
/// <param name="Messages">消息列表（按时间顺序）</param>
/// <param name="Temperature">随机性/发散度（通常 0~2；不同供应商可能范围不同）</param>
/// <param name="MaxTokens">最大生成 token 数（不同供应商含义/字段名可能不同）</param>
/// <param name="User">可选：终端用户标识（用于审计/限流等）</param>
public sealed record ChatRequest(
    string Model,
    IReadOnlyList<ChatMessage> Messages,
    float? Temperature = null,
    int? MaxTokens = null,
    string? User = null
);
