using LLM.Compatible;

namespace LLM;

/// <summary>
/// 基于数据库配置动态创建 LLM 客户端的工厂（无跨进程缓存）
/// </summary>
public sealed class DynamicLlmClientFactory(IHttpClientFactory httpClientFactory) : ILlmClientFactory
{

    /// <summary>
    /// 根据模型 ID 获取对应的 LLM 客户端，每次调用均从配置解析器获取最新配置并创建新客户端实例
    /// </summary>
    public async Task<ILlmClient> GetClientAsync(long modelId, ILlmModelConfigResolver configResolver)
    {
        var config = await configResolver.GetConfigAsync(modelId)
            ?? throw new InvalidOperationException($"LLM model config not found or disabled: {modelId}");

        if (string.IsNullOrWhiteSpace(config.Endpoint))
        {
            throw new InvalidOperationException($"LLM model endpoint is required: {modelId}");
        }

        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            throw new InvalidOperationException($"LLM model api key is required: {modelId}");
        }

        var httpClient = httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(120);
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);

        return new OpenAiCompatibleProviderClient(httpClient, config, config.ModelId);
    }

}
