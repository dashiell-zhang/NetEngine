using Application.Model.Message;
using Application.Service;
using Application.Service.SMS;
using Repository.Database;
using TaskService.Core;
using TaskService.Core.QueueTask;

namespace TaskService.Tasks;

public class MessageTask(MessageService messageService, SmsService smsService) : TaskBase
{

    [QueueTask(Name = "MessageTask.SendSMS", Semaphore = 1)]
    public Task SendSMS(SendSMSDto sendSMS) => smsService.SendSMSAsync(sendSMS);


    [QueueTask(Name = "MessageTask.SendEmail", Semaphore = 1)]
    public Task SendEmail(SendEmailDto email) => messageService.SendEmailAsync(email);

}
