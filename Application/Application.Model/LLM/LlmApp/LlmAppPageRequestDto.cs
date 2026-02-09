using Application.Model.Shared;

namespace Application.Model.LLM.LlmApp;

/// <summary>
/// LLM 应用配置分页请求
/// </summary>
public class LlmAppPageRequestDto : PageRequestDto
{

    /// <summary>
    /// Code/Name/Provider/Model/Remark 模糊检索
    /// </summary>
    public string? Keyword { get; set; }


    /// <summary>
    /// 供应商筛选
    /// </summary>
    public string? Provider { get; set; }


    /// <summary>
    /// 是否启用
    /// </summary>
    public bool? IsEnable { get; set; }
}

