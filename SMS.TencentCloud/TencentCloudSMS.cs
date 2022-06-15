using System.Collections.Generic;
using System.Linq;
using TencentCloud.Common;
using TencentCloud.Sms.V20190711;
using TencentCloud.Sms.V20190711.Models;

namespace SMS.TencentCloud
{
    public class TencentCloudSMS : ISMS
    {

        /// <summary>
        /// SDK AppId (非账号APPId)
        /// </summary>
        private readonly string appId;


        /// <summary>
        /// 账号密钥ID
        /// </summary>
        private readonly string secretId;


        /// <summary>
        /// 账号密钥Key
        /// </summary>
        private readonly string secretKey;



        public TencentCloudSMS(string appId, string secretId, string secretKey)
        {
            this.appId = appId;
            this.secretId = secretId;
            this.secretKey = secretKey;
        }



        public bool SendSMS(string signName, string phone, string templateCode, Dictionary<string, string> templateParams)
        {
            try
            {
                var templateParamsArray = templateParams.Select(t => t.Value).ToArray();

                Credential cred = new()
                {
                    SecretId = secretId,
                    SecretKey = secretKey
                };

                SmsClient client = new(cred, "ap-guangzhou");

                SendSmsRequest req = new()
                {
                    SmsSdkAppid = appId,

                    Sign = signName,

                    PhoneNumberSet = new string[] { "+86" + phone },

                    TemplateID = templateCode,

                    TemplateParamSet = templateParamsArray
                };


                client.SendSms(req);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
