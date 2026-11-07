namespace Application.Model.LLM.Invocation;

/// <summary>
/// LLM 调用的 Token 用量统计
/// </summary>
/// <param name="PromptTokens">提示词 Token 数</param>
/// <param name="CompletionTokens">生成 Token 数</param>
/// <param name="TotalTokens">总 Token 数</param>
public sealed record LlmTokenUsageDto(int? PromptTokens = null, int? CompletionTokens = null, int? TotalTokens = null);
