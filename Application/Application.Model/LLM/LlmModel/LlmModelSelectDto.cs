namespace Application.Model.LLM.LlmModel;

/// <summary>
/// LLM 模型下拉选项
/// </summary>
public class LlmModelSelectDto
{

    /// <summary>
    /// 模型 ID
    /// </summary>
    public long Id { get; set; }


    /// <summary>
    /// 模型显示名称
    /// </summary>
    public string Name { get; set; }


    /// <summary>
    /// 请求使用的模型标识
    /// </summary>
    public string ModelId { get; set; } = string.Empty;


    /// <summary>
    /// 关联模型的协议类型
    /// </summary>
    public int ProtocolType { get; set; }

}
