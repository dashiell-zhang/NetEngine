namespace LLM.Qwen.Models;

public sealed class QwenSetting
{
    /// <summary>
    /// DashScope OpenAI-compatible base url，例如：
    /// - https://dashscope.aliyuncs.com/compatible-mode/v1
    /// - https://dashscope-us.aliyuncs.com/compatible-mode/v1
    /// </summary>
    public string BaseUrl { get; set; } = "https://dashscope-us.aliyuncs.com/compatible-mode/v1";

    public string ApiKey { get; set; } = string.Empty;

    public string? DefaultModel { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);
}

