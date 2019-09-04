using Alipay.AopSdk.Core;
using Alipay.AopSdk.Core.Domain;
using Alipay.AopSdk.Core.Request;
using Alipay.AopSdk.Core.Response;
using System;
using System.Collections.Generic;
using System.Text;

namespace Methods.AliPay.MiniApp
{
    public class AliPayHelper
    {

        /// <summary>
        /// AppId
        /// </summary>
        public string appid;


        /// <summary>
        /// 应用私钥
        /// </summary>
        public string appprivatekey;


        /// <summary>
        /// 支付宝公钥
        /// </summary>
        public string alipaypublickey;



        /// <param name="in_appid">AppId</param>
        /// <param name="in_appprivatekey">应用私钥</param>
        /// <param name="in_alipaypublickey">支付宝公钥</param>
        public AliPayHelper(string in_appid, string in_appprivatekey, string in_alipaypublickey)
        {
            appid = in_appid;
            appprivatekey = in_appprivatekey;
            alipaypublickey = in_alipaypublickey;
        }




        /// <summary>
        /// 通过Code获取支付宝UserId
        /// </summary>
        /// <param name="code">前端获取的临时code</param>
        /// <returns></returns>
        public string GetUserId(string code)
        {
            try
            {
                IAopClient client = new DefaultAopClient("https://openapi.alipay.com/gateway.do", appid, appprivatekey, "json", "1.0", "RSA2", alipaypublickey, "utf-8", false);
                AlipaySystemOauthTokenRequest request = new AlipaySystemOauthTokenRequest();
                request.GrantType = "authorization_code";
                request.Code = code;
                AlipaySystemOauthTokenResponse response = client.Execute(request);

                var body = response.Body;

                var userid = Json.JsonHelper.GetValueByKey(Json.JsonHelper.GetValueByKey(body, "alipay_system_oauth_token_response"), "user_id");

                return userid;
            }
            catch
            {
                return null;
            }
        }

    }
}
