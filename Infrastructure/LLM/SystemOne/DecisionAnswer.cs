namespace LLM.SystemOne;

/// <summary>
/// System One 决策答案
/// </summary>
public sealed class DecisionAnswer
{

    /// <summary>
    /// 决策类型
    /// </summary>
    public string Type { get; set; } = string.Empty;


    /// <summary>
    /// 选中的分类选项
    /// </summary>
    public string? Choice { get; set; }


    /// <summary>
    /// 是否判断中为真的概率
    /// </summary>
    public double? Noul { get; set; }


    /// <summary>
    /// 评分等级索引的概率加权值
    /// </summary>
    public double? Score { get; set; }


    /// <summary>
    /// 概率分布集中程度
    /// </summary>
    public double? Confidence { get; set; }


    /// <summary>
    /// 各选项或等级的概率
    /// </summary>
    public Dictionary<string, double>? Probabilities { get; set; }


    /// <summary>
    /// 评分等级说明
    /// </summary>
    public Dictionary<string, string>? Legend { get; set; }

}
