namespace Application.Model.Task.Message;
public class SendSMSDto
{

    /// <summary>
    /// 短信签名
    /// </summary>
    public string SignName { get; set; }


    /// <summary>
    /// 手机号
    /// </summary>
    public string Phone { get; set; }


    /// <summary>
    /// 模板编号
    /// </summary>
    public string TemplateCode { get; set; }


    /// <summary>
    /// 模板参数
    /// </summary>
    public Dictionary<string, string> TemplateParams { get; set; }

}
