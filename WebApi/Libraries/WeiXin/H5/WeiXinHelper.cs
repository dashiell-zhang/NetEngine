using System;
using System.Collections.Generic;
using System.Text;
using WebApi.Libraries.Http;
using System.Linq;
using System.Security.Cryptography;
using WebApi.Libraries.WeiXin.H5.Models;
using Methods.Json;

namespace WebApi.Libraries.WeiXin.H5
{

    public class WeiXinHelper
    {


        private static string appid;

        private static string appsecret;



        /// <summary>
        /// 微信H5网页开发帮助类
        /// </summary>
        /// <param name="in_appid">微信公众号APPID</param>
        /// <param name="in_appsecret">微信公众号密钥</param>
        public WeiXinHelper(string in_appid, string in_appsecret)
        {
            appid = in_appid;
            appsecret = in_appsecret;
        }



        /// <summary>
        /// 获取 AccessToken
        /// </summary>
        /// <returns></returns>
        public string GetAccessToken()
        {

            string url = "https://api.weixin.qq.com/cgi-bin/token?grant_type=client_credential&appid=" + appid + "&secret=" + appsecret;

            var returnJson = Methods.Http.Post.Run(url, "", "form");

            var token = JsonHelper.GetValueByKey(returnJson, "access_token");

            return token;
        }



        /// <summary>
        /// 获取 TicketID
        /// </summary>
        /// <returns></returns>
        private string GetTicketID()
        {

            string getUrl = "https://api.weixin.qq.com/cgi-bin/ticket/getticket?access_token=" + GetAccessToken() + "&type=jsapi";

            string returnJson = Methods.Http.Post.Run(getUrl, "", "form");

            var ticketid = JsonHelper.GetValueByKey(returnJson, "ticket");

            return ticketid;
        }



        /// <summary>
        /// 获取 JsSDK 签名信息
        /// </summary>
        /// <returns></returns>
        public WeiXinJsSdkSign GetJsSDKSign()
        {
            WeiXinJsSdkSign sdkSign = new WeiXinJsSdkSign();

            sdkSign.appid = appid;
            string url = HttpContext.GetUrl();
            sdkSign.timestamp = Methods.DataTime.DateTimeHelper.TimeToUnix(DateTime.Now);
            sdkSign.noncestr = Guid.NewGuid().ToString().Replace("-", "");
            string jsapi_ticket = GetTicketID();

            string strYW = "jsapi_ticket=" + jsapi_ticket + "&noncestr=" + sdkSign.noncestr + "&timestamp=" + sdkSign.timestamp + "&url=" + url;

            SHA1 sha1 = new SHA1CryptoServiceProvider();
            byte[] bytes_sha1_in = System.Text.UTF8Encoding.Default.GetBytes(strYW);
            byte[] bytes_sha1_out = sha1.ComputeHash(bytes_sha1_in);
            string str_sha1_out = BitConverter.ToString(bytes_sha1_out);
            str_sha1_out = str_sha1_out.Replace("-", "").ToLower();


            sdkSign.signature = str_sha1_out;

            return sdkSign;
        }

    }

}
