using System;
using System.Collections.Generic;
using System.Text;
using Methods.Http;

namespace Methods.WeiXin.MiniApp
{
    public class WeiXinHelper
    {
        public string appid;

        public string secret;

        public WeiXinHelper(string in_appid, string in_secret)
        {
            appid = in_appid;
            secret = in_secret;
        }

        public string GetOpenId(string code)
        {

            string url = "https://api.weixin.qq.com/sns/jscode2session?appid=" + appid + "&secret=" + secret + "&js_code=" + code + "&grant_type=authorization_code";

            string httpret = Get.Run(url);

            tempClass tempClass = Json.JsonHelper.JSONToObject<tempClass>(httpret);

            return tempClass.openid;
        }

        private class tempClass
        {
            public string session_key { get; set; }

            public string openid { get; set; }
        }
    }
}
