using System;
using System.Collections.Generic;
using System.Text;

namespace Methods.WeiXin.Web
{
    public class WeiXinHelper
    {
        /// <summary>
        /// 获取微信OpenID
        /// </summary>
        /// <returns></returns>
        public static string GetOpenID()
        {
            //using (cckdwebContext db = new cckdwebContext())
            //{
            //    int cityid = HttpContext.Current().Session.GetInt32("cityid").Value;

            //    var payconf = db.TPayConf.Where(t => t.Type == "微信" & t.Cityid == cityid).FirstOrDefault();

            //    string nurl = HttpContext.GetUrl();


            //    HttpContext.Current().Request.Cookies.TryGetValue("openid", out string openid);

            //    if (string.IsNullOrEmpty(openid))
            //    {
            //        string code = HttpContext.Current().Request.Query["code"];
            //        if (string.IsNullOrEmpty(code))
            //        {
            //            HttpContext.Current().Response.Redirect("https://open.weixin.qq.com/connect/oauth2/authorize?appid=" + payconf.Key3 + "&redirect_uri=" + nurl + "&response_type=code&scope=snsapi_base&state=Internal#wechat_redirect");
            //        }
            //        else
            //        {
            //            string getUrl = "https://api.weixin.qq.com/sns/oauth2/access_token?appid={0}&secret={1}&code={2}&grant_type=authorization_code";
            //            getUrl = string.Format(getUrl, payconf.Key3, payconf.Key4, code);
            //            var returnJson = Run.Post(getUrl, "", "form");

            //            WxJson wx = JsonHelper.JSONToObject<WxJson>(returnJson);

            //            openid = wx.openid;

            //            HttpContext.Current().Response.Cookies.Append("openid", openid);
            //        }
            //    }
            //    return openid;
            //}

            return null;
        }
        public class WxJson
        {
            public string access_token { get; set; }
            public int expires_in { get; set; }
            public string refresh_token { get; set; }
            public string openid { get; set; }
            public string scope { get; set; }
        }






        /// <summary>
        /// 唤起微信支付
        /// </summary>
        /// <returns></returns>
        public static (string wxjson, string returl) NewWxPay(/*TNewOrder order*/)
        {
            //string host = HttpContext.Current().Request.Host.Host;


            //string redirect_url = "http://" + host + "/New/Step3";
            //string notify_url = "http://" + host + "/New/WxPayNotify";

            //var bossinfo = Run.OrderNoGetBossPackage(order.Orderno);

            //string sendUrl = "https://api.mch.weixin.qq.com/pay/unifiedorder";

            //JsApiConfig jsApiConfig = new JsApiConfig();

            //WxPayData data = new WxPayData();

            //data.SetValue("body", bossinfo.Bossname); //商品描述

            //data.SetValue("detail", "长城宽带-" + bossinfo.Bossname); //商品详情

            //data.SetValue("out_trade_no", order.Orderno); //商户订单号

            //data.SetValue("total_fee", (Convert.ToDouble(order.Price) * 100).ToString()); //订单总金额,以分为单位

            //data.SetValue("trade_type", "JSAPI"); //交易类型

            //HttpContext.Current().Request.Cookies.TryGetValue("openid", out string openid);

            //data.SetValue("openid", openid); //公众账号ID

            //data.SetValue("appid", jsApiConfig.AppId); //公众账号ID

            //data.SetValue("mch_id", jsApiConfig.Partner); //商户号

            //data.SetValue("nonce_str", JsApiPay.GenerateNonceStr()); //随机字符串

            //data.SetValue("notify_url", notify_url); //异步通知url

            //data.SetValue("spbill_create_ip", HttpContext.Current().Connection.RemoteIpAddress.ToString()); //终端IP

            //data.SetValue("sign", data.MakeSign(jsApiConfig.Key)); //签名

            //string xml = data.ToXml(); //转换成XML

            //var startTime = DateTime.Now; //开始时间

            //string response = HttpService.Post(xml, sendUrl, false, 6); //发送请求

            //var endTime = DateTime.Now; //结束时间

            //int timeCost = (int)((endTime - startTime).TotalMilliseconds); //计算所用时间

            //WxPayData result = new WxPayData();

            //result.FromXml(response, jsApiConfig.Key);

            //JsApiPay.ReportCostTime(sendUrl, timeCost, result); //测速上报


            //WxPayData jsApiParam = new WxPayData();

            //jsApiParam.SetValue("appId", result.GetValue("appid"));

            //jsApiParam.SetValue("timeStamp", JsApiPay.GenerateTimeStamp());

            //jsApiParam.SetValue("nonceStr", JsApiPay.GenerateNonceStr());

            //jsApiParam.SetValue("package", "prepay_id=" + result.GetValue("prepay_id"));

            //jsApiParam.SetValue("signType", "MD5");

            //jsApiParam.SetValue("paySign", jsApiParam.MakeSign(jsApiConfig.Key));

            //string wxjson = jsApiParam.ToJson();

            //string returl = redirect_url;


            string wxjson = "";

            string returl = "";

            return (wxjson, returl);
        }
    }
}
