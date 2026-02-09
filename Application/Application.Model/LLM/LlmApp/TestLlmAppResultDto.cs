namespace Application.Model.LLM.LlmApp;

/// <summary>
/// LLM 调用测试结果
/// </summary>
public class TestLlmAppResultDto
{

    /// <summary>
    /// 实际使用的模型名称
    /// </summary>
    public string? Model { get; set; }


    /// <summary>
    /// 响应 ID（如有）
    /// </summary>
    public string? ResponseId { get; set; }


    /// <summary>
    /// 返回文本（取第一个 choice）
    /// </summary>
    public string? Content { get; set; }


    /// <summary>
    /// Token 用量（如有）
    /// </summary>
    public TestLlmAppUsageDto? Usage { get; set; }
}

public class TestLlmAppUsageDto
{
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }
}

