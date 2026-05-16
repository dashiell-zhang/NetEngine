using LLM.Compatible;

namespace LLM;

/// <summary>
/// LLM 模型运行时配置
/// </summary>
public sealed record LlmModelConfig : IOpenAiCompatibleSetting
{
    /// <summary>
    /// API 端点地址
    /// </summary>
    public string Endpoint { get; set; }

    /// <summary>
    /// 接口密钥
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// 模型标识，传递给 ChatRequest.Model
    /// </summary>
    public string ModelId { get; init; }

    /// <summary>
    /// 协议类型（0=Chat, 1=Responses 预留）
    /// </summary>
    public int ProtocolType { get; init; }
}
