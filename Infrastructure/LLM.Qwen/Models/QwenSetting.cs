using LLM.Compatible;

namespace LLM.Qwen.Models;

public sealed class QwenSetting : IOpenAiCompatibleSetting
{

    public string BaseUrl { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1";

    public string ApiKey { get; set; } = string.Empty;

    public string? DefaultModel { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);
}
