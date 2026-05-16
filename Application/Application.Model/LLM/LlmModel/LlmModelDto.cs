namespace Application.Model.LLM.LlmModel;

/// <summary>
/// LLM 模型配置
/// </summary>
public class LlmModelDto
{

    /// <summary>
    /// 标识ID
    /// </summary>
    public long Id { get; set; }


    /// <summary>
    /// 模型显示名称
    /// </summary>
    public string Name { get; set; }


    /// <summary>
    /// 模型标识
    /// </summary>
    public string ModelId { get; set; }


    /// <summary>
    /// API 端点地址
    /// </summary>
    public string Endpoint { get; set; }


    /// <summary>
    /// 接口密钥
    /// </summary>
    public string ApiKey { get; set; }


    /// <summary>
    /// 协议类型（0=Chat, 1=Responses）
    /// </summary>
    public int ProtocolType { get; set; }


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
