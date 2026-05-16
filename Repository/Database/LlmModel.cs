using Microsoft.EntityFrameworkCore;
using Repository.Database.Bases;

namespace Repository.Database;

/// <summary>
/// LLM 模型配置表
/// </summary>
[Index(nameof(IsEnable))]
public class LlmModel : CUD_User
{

    /// <summary>
    /// 模型显示名称（如 "DeepSeek V3"）
    /// </summary>
    public string Name { get; set; }


    /// <summary>
    /// 模型标识（如 "deepseek-chat"），传递给 ChatRequest.Model
    /// </summary>
    public string ModelId { get; set; }


    /// <summary>
    /// API 端点地址（完整 URL）
    /// </summary>
    public string Endpoint { get; set; }


    /// <summary>
    /// 接口密钥
    /// </summary>
    public string ApiKey { get; set; }


    /// <summary>
    /// 协议类型（0=Chat, 1=Responses 预留）
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
