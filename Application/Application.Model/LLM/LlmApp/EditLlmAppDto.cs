namespace Application.Model.LLM.LlmApp;

/// <summary>
/// 编辑 LLM 应用配置
/// </summary>
public class EditLlmAppDto
{

    /// <summary>
    /// 应用标记
    /// </summary>
    public string Code { get; set; }


    /// <summary>
    /// 应用名称
    /// </summary>
    public string Name { get; set; }


    /// <summary>
    /// 绑定的 LLM 供应商标识（如 DeepSeek、Qwen）
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
    /// 最大生成 token 数（不同供应商字段名/含义可能略有不同）
    /// </summary>
    public int? MaxTokens { get; set; }


    /// <summary>
    /// 随机性/发散度（通常 0~2；不同供应商可能范围不同）
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// 额外请求参数（JSON对象字符串，直接透传到 OpenAI-compatible body 根字段）
    /// </summary>
    public string? ExtraBodyJson { get; set; }


    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnable { get; set; } = true;


    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}
