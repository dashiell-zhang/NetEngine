namespace LLM;

/// <summary>
/// LLM 客户端工厂，根据模型 ID 动态获取对应的客户端实现
/// </summary>
public interface ILlmClientFactory
{

    /// <summary>
    /// 根据模型 ID 获取对应的 LLM 客户端
    /// </summary>
    /// <param name="modelId">模型 ID</param>
    /// <param name="configResolver">模型配置解析器</param>
    /// <returns>对应的 LLM 客户端</returns>
    Task<ILlmClient> GetClientAsync(long modelId, ILlmModelConfigResolver configResolver);


    /// <summary>
    /// 清除指定模型的客户端缓存（模型配置更新或删除时调用）
    /// </summary>
    /// <param name="modelId">模型 ID</param>
    void InvalidateCache(long modelId);

}
