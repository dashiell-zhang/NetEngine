using LLM.Compatible;
using LLM.Qwen.Models;
using Microsoft.Extensions.Options;

namespace LLM.Qwen;

public sealed class QwenLlmClient(HttpClient httpClient, IOptions<QwenSetting> options)
    : OpenAiCompatibleLlmClient<QwenSetting>(httpClient, options.Value)
{
    protected override string ProviderName => "Qwen";
}
