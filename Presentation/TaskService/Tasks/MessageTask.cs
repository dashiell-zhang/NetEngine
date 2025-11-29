using Application.Model.Task.Message;
using Common;
using FileStorage;
using Microsoft.EntityFrameworkCore;
using Repository.Database;
using SMS;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using TaskService.Core;
using TaskService.Core.QueueTask;

namespace TaskService.Tasks;
public class MessageTask(IHostEnvironment hostEnvironment, ILogger<MessageTask> logger, DatabaseContext db, IFileStorage fileStorage, ISMS sms) : TaskBase
{

    private readonly string rootPath = hostEnvironment.ContentRootPath;


    [QueueTask(Name = "MessageTask.SendSMS", Semaphore = 1)]
    public async Task SendSMS(DtoSendSMS sendSMS)
    {
        await sms.SendSMSAsync(sendSMS.SignName, sendSMS.Phone, sendSMS.TemplateCode, sendSMS.TemplateParams);
    }



    [QueueTask(Name = "MessageTask.SendEmail", Semaphore = 1)]
    public async Task SendEmail(DtoSendEmail email)
    {

        var smtpServer = "";    //如：网易 smtp.qiye.163.com
        var accountName = "";
        var accountPassword = "";

        Dictionary<string, string> filePaths = [];

        if (email.FileIdList != null)
        {
            var files = await db.TFile.Where(t => email.FileIdList.Contains(t.Id)).Select(t => new { t.Id, t.Name, t.Path }).ToListAsync();

            foreach (var fileId in email.FileIdList)
            {
                var file = files.Where(t => t.Id == fileId).Select(t => new { t.Name, t.Path }).First();

                var localPath = Path.Combine(rootPath, file.Path);
                var directoryName = Path.GetDirectoryName(localPath)!;

                if (!Directory.Exists(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }

                var downloadState = await fileStorage.FileDownloadAsync(file.Path, localPath);

                if (downloadState)
                {
                    filePaths.Add(localPath, file.Name);
                }
                else
                {
                    logger.LogError(fileId + "文件下载失败");
                    throw new Exception(fileId + "文件下载失败");
                }
            }
        }

        using SmtpClient smtpClient = new(smtpServer, 587);
        smtpClient.Credentials = new NetworkCredential(accountName, accountPassword);
        smtpClient.EnableSsl = true;

        using (MailMessage message = new())
        {
            foreach (var filePath in filePaths)
            {
                Attachment data = new(filePath.Key, MediaTypeNames.Application.Octet)
                {
                    Name = filePath.Value
                };
                message.Attachments.Add(data);
            }

            message.From = new(accountName, email.FromDisplayName);

            foreach (var toAddress in email.ToAddresses)
            {
                message.To.Add(new MailAddress(toAddress));
            }

            message.Subject = email.Subject;
            message.Body = email.Body;

            message.IsBodyHtml = email.IsBodyHtml;

            await smtpClient.SendMailAsync(message);
        }

        foreach (var filePath in filePaths)
        {
            IOHelper.DeleteFile(filePath.Key);
        }

    }
}
