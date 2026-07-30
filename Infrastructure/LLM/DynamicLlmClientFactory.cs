using LLM.Anthropic;
using LLM.Compatible;
using LLM.Responses;

namespace LLM;

/// <summary>
/// 基于运行时配置动态创建 LLM 客户端的工厂
/// </summary>
public sealed class DynamicLlmClientFactory(IHttpClientFactory httpClientFactory) : ILlmClientFactory
{

    /// <summary>
    /// 根据模型运行时配置创建新的 LLM 客户端实例
    /// </summary>
    public ILlmClient CreateClient(LlmModelConfig config)
    {

        if (string.IsNullOrWhiteSpace(config.Endpoint))
        {
            throw new InvalidOperationException($"LLM model endpoint is required: {config.ModelId}");
        }

        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            throw new InvalidOperationException($"LLM model api key is required: {config.ModelId}");
        }

        var httpClient = httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(120);

        return (LlmProtocolType)config.ProtocolType switch
        {
            LlmProtocolType.Chat => CreateOpenAiCompatibleClient(httpClient, config),
            LlmProtocolType.Responses => CreateOpenAiResponsesClient(httpClient, config),
            LlmProtocolType.Anthropic => new AnthropicMessagesLlmClient(httpClient, config),
            _ => throw new InvalidOperationException($"Unsupported LLM protocol type: {config.ProtocolType}")
        };

    }


    /// <summary>
    /// 创建 OpenAI-Compatible 客户端
    /// </summary>
    private static ILlmClient CreateOpenAiCompatibleClient(HttpClient httpClient, LlmModelConfig config)
    {

        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);
        return new OpenAiCompatibleProviderClient(httpClient, config, config.ModelId);
    }


    /// <summary>
    /// 创建 OpenAI Responses 客户端
    /// </summary>
    private static ILlmClient CreateOpenAiResponsesClient(HttpClient httpClient, LlmModelConfig config)
    {

        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);
        return new OpenAiResponsesLlmClient(httpClient, config);
    }

}
