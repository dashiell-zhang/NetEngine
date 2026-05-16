using Application.Model.Shared;

namespace Application.Model.LLM.LlmModel;

/// <summary>
/// LLM 模型配置分页请求
/// </summary>
public class LlmModelPageRequestDto : PageRequestDto
{

    /// <summary>
    /// 名称/模型标识/备注 模糊检索
    /// </summary>
    public string? Keyword { get; set; }


    /// <summary>
    /// 是否启用
    /// </summary>
    public bool? IsEnable { get; set; }

}
