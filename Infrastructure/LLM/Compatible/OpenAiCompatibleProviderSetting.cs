namespace LLM.Compatible;

/// <summary>
/// 通用 OpenAI-Compatible Provider 配置（Endpoint + ApiKey + 默认模型等）
/// 适用于 DeepSeek / Qwen / 豆包（OpenAI 兼容模式）等场景
/// </summary>
public sealed class OpenAiCompatibleProviderSetting : IOpenAiCompatibleSetting
{

    /// <summary>
    /// Provider 的完整接口端点地址 例如 https://host/v1/chat/completions
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;


    /// <summary>
    /// Provider 的访问密钥
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

}
