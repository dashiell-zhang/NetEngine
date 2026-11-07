using Application.Model.Message;

namespace Application.Interface;

/// <summary>
/// 邮件消息发送能力
/// </summary>
public interface IMessageService
{

    /// <summary>
    /// 发送邮件及关联附件
    /// </summary>
    Task SendEmailAsync(SendEmailDto email);

}
