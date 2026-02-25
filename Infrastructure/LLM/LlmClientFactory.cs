using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace LLM;

/// <summary>
/// 基于依赖注入的 LLM 客户端工厂
/// </summary>
public sealed class LlmClientFactory(IServiceProvider serviceProvider) : ILlmClientFactory
{

    /// <summary>
    /// 获取指定 Provider 的客户端
    /// </summary>
    public ILlmClient GetClient(string provider)
    {
        if (!TryGetClient(provider, out var client))
        {
            throw new InvalidOperationException($"No ILlmClient registered for provider '{provider}'.");
        }

        return client;
    }


    /// <summary>
    /// 尝试通过 keyed 服务解析获取客户端
    /// </summary>
    public bool TryGetClient(string provider, [NotNullWhen(true)] out ILlmClient? client)
    {
        client = serviceProvider.GetKeyedService<ILlmClient>(provider);
        return client != null;
    }

}
