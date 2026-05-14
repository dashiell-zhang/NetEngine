using System.ComponentModel.DataAnnotations;

namespace Application.Model.LLM.LlmApp;

/// <summary>
/// 编辑 LLM 应用配置
/// </summary>
public class EditLlmAppDto
{

    /// <summary>
    /// 应用标记
    /// </summary>
    [Required(ErrorMessage = "Code 不可以空")]
    public string Code { get; set; } = string.Empty;


    /// <summary>
    /// 应用名称
    /// </summary>
    [Required(ErrorMessage = "名称不可以空")]
    public string Name { get; set; } = string.Empty;


    /// <summary>
    /// 绑定的 LLM 供应商标识（如 DeepSeek、Qwen）
    /// </summary>
    [Required(ErrorMessage = "供应商不可以空")]
    public string Provider { get; set; } = string.Empty;


    /// <summary>
    /// 模型名称
    /// </summary>
    [Required(ErrorMessage = "模型不可以空")]
    public string Model { get; set; } = string.Empty;


    /// <summary>
    /// System 提示词模板（可包含 {{Key}} 占位符）
    /// </summary>
    public string? SystemPromptTemplate { get; set; }


    /// <summary>
    /// User 提示词模板（可包含 {{Key}} 占位符）
    /// </summary>
    [Required(ErrorMessage = "PromptTemplate 不可以空")]
    public string PromptTemplate { get; set; } = string.Empty;


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
