using LLM.Compatible;
using LLM.DeepSeek.Models;
using Microsoft.Extensions.Options;

namespace LLM.DeepSeek;

public sealed class DeepSeekLlmClient(HttpClient httpClient, IOptions<DeepSeekSetting> options)
    : OpenAiCompatibleLlmClient<DeepSeekSetting>(httpClient, options.Value)
{
    protected override string ProviderName => "DeepSeek";
}
