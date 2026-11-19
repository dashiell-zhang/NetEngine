using System.Text.Json.Serialization;

namespace LLM.SystemOne;

/// <summary>
/// System One 决策响应
/// </summary>
public sealed class DecisionResponse
{

    /// <summary>
    /// 本次调用发送的完整请求正文
    /// </summary>
    [JsonIgnore]
    public string RequestJson { get; internal set; } = string.Empty;


    /// <summary>
    /// 供应商返回的响应原文
    /// </summary>
    [JsonIgnore]
    public string RawContent { get; internal set; } = string.Empty;


    /// <summary>
    /// 实际使用的模型
    /// </summary>
    public string Model { get; set; } = string.Empty;


    /// <summary>
    /// 按问题标识组织的答案
    /// </summary>
    public Dictionary<string, DecisionAnswer> Answers { get; set; } = new(StringComparer.Ordinal);


    /// <summary>
    /// 供应商请求标识
    /// </summary>
    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }


    /// <summary>
    /// 供应商返回的用量
    /// </summary>
    public DecisionUsage? Usage { get; set; }


    /// <summary>
    /// 供应商报告的耗时，单位毫秒
    /// </summary>
    [JsonPropertyName("latency_ms")]
    public double? LatencyMs { get; set; }

}
