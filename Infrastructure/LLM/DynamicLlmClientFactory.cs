using LLM.Compatible;
using System.Collections.Concurrent;

namespace LLM;

/// <summary>
/// 基于数据库配置动态创建 LLM 客户端的工厂
/// </summary>
public sealed class DynamicLlmClientFactory(IHttpClientFactory httpClientFactory) : ILlmClientFactory
{

    private readonly ConcurrentDictionary<long, LlmClientCacheEntry> _clientCache = new();


    /// <summary>
    /// 根据模型 ID 获取对应的 LLM 客户端，优先从缓存中读取
    /// </summary>
    public async Task<ILlmClient> GetClientAsync(long modelId, ILlmModelConfigResolver configResolver)
    {
        if (_clientCache.TryGetValue(modelId, out var cached))
        {
            return cached.Client;
        }

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

        var client = new OpenAiCompatibleProviderClient(httpClient, config, config.ModelId);
        var entry = new LlmClientCacheEntry(client, config);

        _clientCache[modelId] = entry;

        return client;
    }


    /// <summary>
    /// 清除指定模型的客户端缓存（模型配置更新或删除时调用）
    /// </summary>
    public void InvalidateCache(long modelId)
    {
        _clientCache.TryRemove(modelId, out _);
    }


    /// <summary>
    /// 客户端缓存条目
    /// </summary>
    private sealed record LlmClientCacheEntry(OpenAiCompatibleProviderClient Client, LlmModelConfig Config);

}
