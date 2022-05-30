using System.Net.Mail;

namespace Common
{

    public class EmailHelper
    {


        /// <summary>
        /// 邮件发送方法
        /// </summary>
        /// <param name="receiveAccount">收件人账户</param>
        /// <param name="zhuti">标题</param>
        /// <param name="neirong">内容</param>
        public static bool Send(string receiveAccount, string title, string content)
        {

            try
            {

                string sendAccount = "XXXX@qq.com";
                string sendAccountPwd = "密码";

                string server = "smtp.qq.com";

                SmtpClient client = new()
                {
                    Host = server,
                    UseDefaultCredentials = false,
                    Credentials = new System.Net.NetworkCredential(sendAccount, sendAccountPwd),
                    DeliveryMethod = SmtpDeliveryMethod.Network
                };
                MailMessage message = new(sendAccount, receiveAccount)
                {
                    Subject = title,
                    Body = content,
                    BodyEncoding = System.Text.Encoding.UTF8,
                    IsBodyHtml = true
                };
                client.Send(message);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
