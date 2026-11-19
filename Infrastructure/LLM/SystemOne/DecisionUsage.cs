using System.Text.Json.Serialization;

namespace LLM.SystemOne;

/// <summary>
/// 决策调用用量，未返回的计量保持为空
/// </summary>
public sealed class DecisionUsage
{

    /// <summary>
    /// 输入 Token 数
    /// </summary>
    [JsonPropertyName("input_tokens")]
    public int? InputTokens { get; set; }


    /// <summary>
    /// 输出 Token 数
    /// </summary>
    [JsonPropertyName("output_tokens")]
    public int? OutputTokens { get; set; }

}
