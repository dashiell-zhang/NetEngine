namespace LLM.Compatible;

/// <summary>
/// OpenAI 兼容 Provider 的基础配置
/// </summary>
public interface IOpenAiCompatibleSetting
{

    /// <summary>
    /// OpenAI 兼容接口的完整端点地址 例如 https://host/v1/chat/completions
    /// </summary>
    string Endpoint { get; set; }


    /// <summary>
    /// OpenAI 兼容接口的访问密钥
    /// </summary>
    string ApiKey { get; set; }

}
