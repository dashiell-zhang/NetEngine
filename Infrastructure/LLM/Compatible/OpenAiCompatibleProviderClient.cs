namespace LLM.Compatible;

/// <summary>
/// 基于 OpenAI-Compatible 协议的通用 Provider 客户端
/// </summary>
public sealed class OpenAiCompatibleProviderClient(HttpClient httpClient, OpenAiCompatibleProviderSetting settings, string providerName)
    : OpenAiCompatibleLlmClient<OpenAiCompatibleProviderSetting>(httpClient, settings)
{

    /// <summary>
    /// Provider 的名称 用于异常信息与可观测性
    /// </summary>
    protected override string ProviderName { get; } = providerName;

}
