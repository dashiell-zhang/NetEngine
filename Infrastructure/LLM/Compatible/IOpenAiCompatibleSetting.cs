namespace LLM.Compatible;

/// <summary>
/// OpenAI 兼容 Provider 的基础配置
/// </summary>
public interface IOpenAiCompatibleSetting
{

    /// <summary>
    /// OpenAI 兼容接口的服务地址基址 通常包含 v1 路径
    /// </summary>
    string BaseUrl { get; set; }


    /// <summary>
    /// OpenAI 兼容接口的访问密钥
    /// </summary>
    string ApiKey { get; set; }

}
