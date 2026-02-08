namespace LLM;

/// <summary>
/// Token 用量统计（字段可能随供应商/接口版本变化）
/// </summary>
/// <param name="PromptTokens">提示词 token 数</param>
/// <param name="CompletionTokens">生成 token 数</param>
/// <param name="TotalTokens">总 token 数</param>
public sealed record Usage(
    int? PromptTokens = null,
    int? CompletionTokens = null,
    int? TotalTokens = null
);
