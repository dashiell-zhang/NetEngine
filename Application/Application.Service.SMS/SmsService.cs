using Application.Model.Message;
using Microsoft.Extensions.DependencyInjection;
using SMS;
using SourceGenerator.Runtime.Attributes;

namespace Application.Service.SMS;

/// <summary>
/// 短信发送服务
/// </summary>
[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class SmsService(ISMS sms)
{

    /// <summary>
    /// 发送短信
    /// </summary>
    public Task SendSMSAsync(SendSMSDto sendSMS)
    {

        return sms.SendSMSAsync(sendSMS.SignName, sendSMS.Phone, sendSMS.TemplateCode, sendSMS.TemplateParams);
    }
}
