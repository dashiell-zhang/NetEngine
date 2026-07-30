namespace LLM;

/// <summary>
/// LLM 客户端工厂，根据运行时配置创建对应的客户端实现
/// </summary>
public interface ILlmClientFactory
{

    /// <summary>
    /// 根据模型运行时配置创建对应的 LLM 客户端
    /// </summary>
    /// <param name="config">模型运行时配置</param>
    /// <returns>对应的 LLM 客户端</returns>
    ILlmClient CreateClient(LlmModelConfig config);

}
