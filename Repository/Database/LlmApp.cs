using Microsoft.EntityFrameworkCore;
using Repository.Database.Bases;
using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.Database;

/// <summary>
/// LLM 应用配置表
/// </summary>
[Index(nameof(Code))]
[Index(nameof(IsEnable))]
public class LlmApp : CUD_User
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
    /// 关联的 LLM 模型 ID
    /// </summary>
    [ForeignKey(nameof(LlmModel))]
    public long LlmModelId { get; set; }


    /// <summary>
    /// 关联的 LLM 模型
    /// </summary>
    public LlmModel LlmModel { get; set; }


    /// <summary>
    /// System 提示词模板（可包含 {{Key}} 占位符）
    /// </summary>
    public string? SystemPromptTemplate { get; set; }


    /// <summary>
    /// User 提示词模板（可包含 {{Key}} 占位符）
    /// </summary>
    public string PromptTemplate { get; set; }


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
