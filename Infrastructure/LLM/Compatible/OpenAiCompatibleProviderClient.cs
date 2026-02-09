namespace LLM.Compatible;

public sealed class OpenAiCompatibleProviderClient(HttpClient httpClient, OpenAiCompatibleProviderSetting settings, string providerName)
    : OpenAiCompatibleLlmClient<OpenAiCompatibleProviderSetting>(httpClient, settings)
{
    protected override string ProviderName { get; } = providerName;
}

