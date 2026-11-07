namespace Application.Model.LLM.Invocation;

/// <summary>
/// LLM 应用对话的完整响应
/// </summary>
/// <param name="Model">实际使用的模型名称</param>
/// <param name="Choices">候选结果列表</param>
/// <param name="Usage">可选的 Token 用量统计</param>
/// <param name="Id">可选的响应标识</param>
public sealed record LlmChatResponseDto(string Model, IReadOnlyList<LlmChatChoiceDto> Choices, LlmTokenUsageDto? Usage = null, string? Id = null);
