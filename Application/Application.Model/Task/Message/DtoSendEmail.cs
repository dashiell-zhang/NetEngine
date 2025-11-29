namespace Application.Model.Task.Message;
/// <summary>
/// 邮件发送模型
/// </summary>
public class DtoSendEmail
{

    /// <summary>
    /// 发件昵称
    /// </summary>
    public string? FromDisplayName { get; set; }


    /// <summary>
    /// 收件人
    /// </summary>
    public List<string> ToAddresses { get; set; }


    /// <summary>
    /// 标题
    /// </summary>
    public string Subject { get; set; }


    /// <summary>
    /// 内瓤
    /// </summary>
    public string Body { get; set; }


    /// <summary>
    /// 内容是否为Html
    /// </summary>
    public bool IsBodyHtml { get; set; }


    /// <summary>
    /// 附件Id集合
    /// </summary>
    public List<long>? FileIdList { get; set; }

}
