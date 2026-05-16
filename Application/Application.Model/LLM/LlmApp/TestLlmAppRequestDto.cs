using System.Text.Json.Nodes;

namespace Application.Model.LLM.LlmApp;

/// <summary>
/// LLM 调用测试请求
/// </summary>
public class TestLlmAppRequestDto
{

    /// <summary>
    /// 关联的 LLM 模型 ID
    /// </summary>
    public long LlmModelId { get; set; }


    /// <summary>
    /// System 提示词模板（可包含 {{Key}} 占位符）
    /// </summary>
    public string? SystemPromptTemplate { get; set; }


    /// <summary>
    /// User 提示词模板（可包含 {{Key}} 占位符）
    /// </summary>
    public string PromptTemplate { get; set; }


    /// <summary>
    /// 额外请求参数（直接透传到 OpenAI-compatible body 根字段）
    /// </summary>
    public Dictionary<string, JsonNode>? ExtraBody { get; set; }


    /// <summary>
    /// 占位符参数（Key -> Value）
    /// </summary>
    public Dictionary<string, string>? Parameters { get; set; }

}
