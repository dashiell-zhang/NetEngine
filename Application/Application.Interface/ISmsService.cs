using Application.Model.Message;

namespace Application.Interface;

/// <summary>
/// 短信消息发送能力
/// </summary>
public interface ISmsService
{

    /// <summary>
    /// 按短信模板发送消息
    /// </summary>
    Task SendSMSAsync(SendSMSDto sendSMS);

}
