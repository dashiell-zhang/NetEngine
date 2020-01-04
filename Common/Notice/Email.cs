using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Text;

namespace Common.Notice
{
    public class Email
    {
        /// <summary>
        /// 邮件发送方法传入接收地址，邮件主题、内容
        /// </summary>
        /// <param name="jiename"></param>
        /// <param name="zhuti"></param>
        /// <param name="neirong"></param>
        public static void Send(string jiename, string zhuti, string neirong)
        {
            string faname = "XXXX@qq.com";
            string famima = "密码";
            string server = "smtp.qq.com";

            System.Net.Mail.SmtpClient client = new SmtpClient();
            client.Host = server;
            client.UseDefaultCredentials = false;
            client.Credentials = new System.Net.NetworkCredential(faname, famima);
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            System.Net.Mail.MailMessage message = new MailMessage(faname, jiename);
            message.Subject = zhuti;
            message.Body = neirong;
            message.BodyEncoding = System.Text.Encoding.UTF8;
            message.IsBodyHtml = true;
            client.Send(message);
        }
    }
}
