using LLM.Anthropic;
using LLM.Compatible;
using LLM.Responses;
using LLM.SystemOne;

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
            LlmProtocolType.SystemOne => throw new InvalidOperationException("决策模型请使用 DecideAsync，不支持聊天或流式调用"),
            LlmProtocolType.Chat => CreateOpenAiCompatibleClient(httpClient, config),
            LlmProtocolType.Responses => CreateOpenAiResponsesClient(httpClient, config),
            LlmProtocolType.Anthropic => new AnthropicMessagesLlmClient(httpClient, config),
            _ => throw new InvalidOperationException($"Unsupported LLM protocol type: {config.ProtocolType}")
        };

    }


    /// <summary>
    /// 创建 System One 决策客户端
    /// </summary>
    public ILlmDecisionClient CreateDecisionClient(LlmModelConfig config)
    {

        if (config.ProtocolType != (int)LlmProtocolType.SystemOne)
        {
            throw new InvalidOperationException("只有 System One 模型支持决策调用");
        }

        if (!Uri.TryCreate(config.Endpoint, UriKind.Absolute, out var endpoint) || (endpoint.Scheme != "https" && endpoint.Scheme != "http") || string.IsNullOrWhiteSpace(config.ApiKey) || string.IsNullOrWhiteSpace(config.ModelId))
        {
            throw new InvalidOperationException("决策模型的 Endpoint、ApiKey 和 ModelId 配置无效");
        }

        var httpClient = httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(120);
        return new SystemOneDecisionClient(httpClient, config);

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
