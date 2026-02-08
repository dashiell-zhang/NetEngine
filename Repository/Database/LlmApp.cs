using Microsoft.EntityFrameworkCore;
using Repository.Database.Bases;

namespace Repository.Database;

/// <summary>
/// LLM 应用配置表（用于按 Code 管理提示词与模型/供应商参数）
/// </summary>
[Index(nameof(Code), IsUnique = true)]
[Index(nameof(Provider))]
[Index(nameof(IsEnable))]
public class LlmApp : CUD_User
{

    /// <summary>
    /// 应用标记（业务侧通过该 Code 调用 LLM）
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
    /// 模型名称（如 deepseek-chat、qwen-plus 等）
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
    /// 是否启用
    /// </summary>
    public bool IsEnable { get; set; } = true;


    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}

