namespace Application.Model.LLM.Decision;

/// <summary>
/// 结构化决策调用结果
/// </summary>
public sealed class LlmDecisionResponseDto
{

    /// <summary>
    /// 实际使用的模型
    /// </summary>
    public string Model { get; set; } = string.Empty;


    /// <summary>
    /// 以问题标识索引的答案
    /// </summary>
    public Dictionary<string, LlmDecisionAnswerDto> Answers { get; set; } = new(StringComparer.Ordinal);


    /// <summary>
    /// 供应商请求标识
    /// </summary>
    public string? RequestId { get; set; }


    /// <summary>
    /// 输入 Token 数
    /// </summary>
    public int? InputTokens { get; set; }


    /// <summary>
    /// 输出 Token 数
    /// </summary>
    public int? OutputTokens { get; set; }


    /// <summary>
    /// 供应商报告的耗时，单位毫秒
    /// </summary>
    public double? LatencyMs { get; set; }

}
