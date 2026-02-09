using LLM.Compatible;

namespace LLM.DeepSeek.Models;

public sealed class DeepSeekSetting : IOpenAiCompatibleSetting
{
    public string BaseUrl { get; set; } = "https://api.deepseek.com";

    public string ApiKey { get; set; } = string.Empty;

    public string? DefaultModel { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);
}
