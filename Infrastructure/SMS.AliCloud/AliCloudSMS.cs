using AlibabaCloud.OpenApiClient.Models;
using AlibabaCloud.SDK.Dysmsapi20170525;
using AlibabaCloud.SDK.Dysmsapi20170525.Models;
using Common;
using Microsoft.Extensions.Options;
using SMS.AliCloud.Models;

namespace SMS.AliCloud
{
    public class AliCloudSMS(IOptionsMonitor<SMSSetting> config) : ISMS
    {


        private readonly string accessKeyId = config.CurrentValue.AccessKeyId;


        private readonly string accessKeySecret = config.CurrentValue.AccessKeySecret;



        public async Task<bool> SendSMSAsync(string signName, string phone, string templateCode, Dictionary<string, string> templateParams)
        {
            try
            {
                string templateParamsJson = JsonHelper.ObjectToJson(templateParams);

                Config config = new()
                {
                    AccessKeyId = accessKeyId,
                    AccessKeySecret = accessKeySecret,
                    Endpoint = "dysmsapi.aliyuncs.com"
                };

                Client client = new(config);

                SendSmsRequest sendSmsRequest = new()
                {
                    PhoneNumbers = phone,
                    SignName = signName,
                    TemplateCode = templateCode,
                    TemplateParam = templateParamsJson,
                };

                AlibabaCloud.TeaUtil.Models.RuntimeOptions runtime = new AlibabaCloud.TeaUtil.Models.RuntimeOptions();


                await client.SendSmsAsync(sendSmsRequest);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
