using Application.Model.Message;
using Application.Interface;
using TaskService.Core;
using TaskService.Core.QueueTask;

namespace TaskService.Tasks;

/// <summary>
/// 将消息队列任务转交给应用服务
/// </summary>
public class MessageTask(IMessageService messageService, ISmsService smsService) : TaskBase
{

    /// <summary>
    /// 执行短信发送任务
    /// </summary>
    [QueueTask(Name = "MessageTask.SendSMS", Semaphore = 1)]
    public Task SendSMS(SendSMSDto sendSMS) => smsService.SendSMSAsync(sendSMS);


    /// <summary>
    /// 执行邮件发送任务
    /// </summary>
    [QueueTask(Name = "MessageTask.SendEmail", Semaphore = 1)]
    public Task SendEmail(SendEmailDto email) => messageService.SendEmailAsync(email);

}
