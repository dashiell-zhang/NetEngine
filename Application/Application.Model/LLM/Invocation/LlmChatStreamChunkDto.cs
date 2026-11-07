namespace Application.Model.LLM.Invocation;

/// <summary>
/// LLM 应用对话的流式响应分片
/// </summary>
/// <param name="Model">实际使用的模型名称</param>
/// <param name="Choices">增量候选列表</param>
/// <param name="Usage">可选的 Token 用量统计</param>
/// <param name="Id">可选的响应标识</param>
public sealed record LlmChatStreamChunkDto(string Model, IReadOnlyList<LlmChatStreamChoiceDto> Choices, LlmTokenUsageDto? Usage = null, string? Id = null);
