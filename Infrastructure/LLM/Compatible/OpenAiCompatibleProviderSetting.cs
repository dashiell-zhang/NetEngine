namespace LLM.Compatible;

/// <summary>
/// 通用 OpenAI-Compatible Provider 配置（BaseUrl + ApiKey + 默认模型等）
/// 适用于 DeepSeek / Qwen / 豆包（OpenAI 兼容模式）等
/// </summary>
public sealed class OpenAiCompatibleProviderSetting : IOpenAiCompatibleSetting
{
    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string? DefaultModel { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);
}

