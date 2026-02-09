namespace Application.Model.LLM.LlmApp;

using System.Text.Json.Nodes;

/// <summary>
/// LLM 调用测试请求
/// </summary>
public class TestLlmAppRequestDto
{

    /// <summary>
    /// 供应商标识（与服务端注册的 ProviderKey 一致）
    /// </summary>
    public string Provider { get; set; }


    /// <summary>
    /// 模型名称
    /// </summary>
    public string Model { get; set; }


    /// <summary>
    /// System 提示词模板（可包含 {{Key}} 占位符）
    /// </summary>
    public string? SystemPromptTemplate { get; set; }


    /// <summary>
    /// User 提示词模板（可包含 {{Key}} 占位符）
    /// </summary>
    public string PromptTemplate { get; set; }


    /// <summary>
    /// 最大生成 token 数
    /// </summary>
    public int? MaxTokens { get; set; }


    /// <summary>
    /// 随机性/发散度
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// 额外请求参数（直接透传到 OpenAI-compatible body 根字段）
    /// </summary>
    public Dictionary<string, JsonNode>? ExtraBody { get; set; }


    /// <summary>
    /// 占位符参数（Key -> Value）
    /// </summary>
    public Dictionary<string, string>? Parameters { get; set; }
}
