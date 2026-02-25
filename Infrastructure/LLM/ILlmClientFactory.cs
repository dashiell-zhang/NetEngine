using System.Diagnostics.CodeAnalysis;

namespace LLM;

/// <summary>
/// LLM 客户端工厂 用于按 Provider 获取对应的客户端实现
/// </summary>
public interface ILlmClientFactory
{

    /// <summary>
    /// 获取指定 Provider 的客户端 若不存在则抛出异常
    /// </summary>
    /// <param name="provider">Provider 标识</param>
    /// <returns>对应的 LLM 客户端</returns>
    ILlmClient GetClient(string provider);


    /// <summary>
    /// 尝试获取指定 Provider 的客户端
    /// </summary>
    /// <param name="provider">Provider 标识</param>
    /// <param name="client">获取到的客户端</param>
    /// <returns>是否成功获取</returns>
    bool TryGetClient(string provider, [NotNullWhen(true)] out ILlmClient? client);

}
