using Aliyun.Acs.Core;
using Aliyun.Acs.Core.Exceptions;
using Aliyun.Acs.Core.Http;
using Aliyun.Acs.Core.Profile;
using System;
using System.Collections.Generic;
using System.Text;

namespace Methods.AliYun
{
    public class SmsHelper
    {
        string accessKeyId = "";
        string accessKeySecret = "";

        public SmsHelper(string in_accessKeyId, string in_accessKeySecret)
        {
            accessKeyId = in_accessKeyId;
            accessKeySecret = in_accessKeySecret;
        }


        /// <summary>
        /// 短信发送方法
        /// </summary>
        /// <param name="Phone">手机号</param>
        /// <param name="TemplateCode">模板编号</param>
        /// <param name="SignName">签名</param>
        /// <param name="TemplateParam">模板中包含得变量值，Json 格式</param>
        /// <returns></returns>
        public bool SendSms(string Phone, string TemplateCode, string SignName, string TemplateParam)
        {
            IClientProfile profile = DefaultProfile.GetProfile("cn-hangzhou", accessKeyId, accessKeySecret);
            DefaultAcsClient client = new DefaultAcsClient(profile);
            CommonRequest request = new CommonRequest();
            request.Method = MethodType.POST;
            request.Domain = "dysmsapi.aliyuncs.com";
            request.AddQueryParameters("PhoneNumbers", Phone);
            request.AddQueryParameters("SignName", SignName);
            request.AddQueryParameters("TemplateCode", TemplateCode);
            request.AddQueryParameters("TemplateParam", TemplateParam);
            request.Version = "2017-05-25";
            request.Action = "SendSms";
            try
            {
                CommonResponse response = client.GetCommonResponse(request);

                string retValue = System.Text.Encoding.Default.GetString(response.HttpResponse.Content);

                return true;
            }
            catch (ServerException e)
            {
                Console.WriteLine(e);

                return false;
            }
            catch (ClientException e)
            {
                Console.WriteLine(e);

                return false;
            }
        }
    }
}
