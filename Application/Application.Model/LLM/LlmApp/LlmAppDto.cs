namespace Application.Model.LLM.LlmApp;

/// <summary>
/// LLM 应用配置
/// </summary>
public class LlmAppDto
{

    /// <summary>
    /// 标识ID
    /// </summary>
    public long Id { get; set; }


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
    public long LlmModelId { get; set; }


    /// <summary>
    /// 关联的 LLM 模型名称
    /// </summary>
    public string LlmModelName { get; set; }


    /// <summary>
    /// System 提示词模板（可包含 {{Key}} 占位符）
    /// </summary>
    public string? SystemPromptTemplate { get; set; }


    /// <summary>
    /// User 提示词模板（可包含 {{Key}} 占位符）
    /// </summary>
    public string PromptTemplate { get; set; }


    /// <summary>
    /// 额外请求参数（JSON对象字符串，直接透传到当前协议请求体根字段）
    /// </summary>
    public string? ExtraBodyJson { get; set; }


    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnable { get; set; }


    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }


    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }


    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTimeOffset? UpdateTime { get; set; }

}
