using System.ComponentModel.DataAnnotations;

namespace Application.Model.LLM.LlmModel;

/// <summary>
/// 编辑 LLM 模型配置
/// </summary>
public class EditLlmModelDto
{

    /// <summary>
    /// 模型显示名称
    /// </summary>
    [Required(ErrorMessage = "名称不可以空")]
    public string Name { get; set; } = string.Empty;


    /// <summary>
    /// 模型标识
    /// </summary>
    [Required(ErrorMessage = "模型标识不可以空")]
    public string ModelId { get; set; } = string.Empty;


    /// <summary>
    /// API 端点地址
    /// </summary>
    [Required(ErrorMessage = "端点地址不可以空")]
    public string Endpoint { get; set; } = string.Empty;


    /// <summary>
    /// 接口密钥
    /// </summary>
    [Required(ErrorMessage = "接口密钥不可以空")]
    public string ApiKey { get; set; } = string.Empty;


    /// <summary>
    /// 协议类型（0=Chat, 1=Responses）
    /// </summary>
    public int ProtocolType { get; set; }


    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnable { get; set; } = true;


    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

}
