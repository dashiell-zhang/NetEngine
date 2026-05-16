namespace LLM;

/// <summary>
/// LLM 模型配置解析器，由 Application 层实现，供 Infrastructure 层使用
/// </summary>
public interface ILlmModelConfigResolver
{

    /// <summary>
    /// 获取指定模型的配置信息
    /// </summary>
    Task<LlmModelConfig?> GetConfigAsync(long modelId, CancellationToken cancellationToken = default);

}
