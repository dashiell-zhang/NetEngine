using Common;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Xml;
using WebApi.Libraries.WeiXin.App.Models;

namespace WebApi.Libraries.WeiXin.App
{
    public class WeiXinHelper
    {
        private readonly string appid;

        private readonly string appsecret;

        private readonly string? mchid;

        private readonly string? mchkey;

        private readonly string? notifyurl;

        public WeiXinHelper(string in_appid, string in_secret, string? in_mchid = null, string? in_mchkey = null, string? in_notifyurl = null)
        {
            appid = in_appid;
            appsecret = in_secret;
            mchid = in_mchid;
            mchkey = in_mchkey;
            notifyurl = in_notifyurl;
        }




        /// <summary>
        /// 微信APP支付商户平台下单方法
        /// </summary>
        /// <param name="httpClientFactory"></param>
        /// <param name="orderno">订单号</param>
        /// <param name="body">商品描述</param>
        /// <param name="price">价格，单位为分</param>
        /// <param name="ip">服务器IP</param>
        /// <returns></returns>
        public DtoCreatePayApp? CreatePay(IHttpClientFactory httpClientFactory, string orderno, string body, int price, string ip)
        {

            string nonceStr = Guid.NewGuid().ToString().Replace("-", "");


            var url = "https://api.mch.weixin.qq.com/pay/unifiedorder";//微信统一下单请求地址




            //参与统一下单签名的参数，除最后的key外，已经按参数名ASCII码从小到大排序
            var unifiedorderSignParam = string.Format("appid={0}&body={1}&mch_id={2}&nonce_str={3}&notify_url={4}&out_trade_no={5}&spbill_create_ip={6}&total_fee={7}&trade_type={8}&key={9}"
                , appid, body, mchid, nonceStr, notifyurl
                , orderno, ip, price, "APP", mchkey);


            var unifiedorderSign = Common.CryptoHelper.GetMD5(unifiedorderSignParam).ToUpper();

            //构造统一下单的请求参数
            var zhi = string.Format(@"<xml>
                                <appid>{0}</appid>                                              
                                <body>{1}</body>
                                <mch_id>{2}</mch_id>   
                                <nonce_str>{3}</nonce_str>
                                <notify_url>{4}</notify_url>
                                <out_trade_no>{5}</out_trade_no>
                                <spbill_create_ip>{6}</spbill_create_ip>
                                <total_fee>{7}</total_fee>
                                <trade_type>{8}</trade_type>
                                <sign>{9}</sign>
                               </xml>
                    ", appid, body, mchid, nonceStr, notifyurl
                              , orderno, ip, price, "APP", unifiedorderSign);




            var getdata = httpClientFactory.Post(url, zhi, "form");

            //获取xml数据
            XmlDocument doc = new();
            doc.LoadXml(getdata);


            //xml格式转json
            string json = Newtonsoft.Json.JsonConvert.SerializeXmlNode(doc);
            JObject jo = (JObject)JsonConvert.DeserializeObject(json)!;


            if (jo["xml"]!["return_code"]!["#cdata-section"]!.ToString() == "SUCCESS")
            {
                string prepay_id = jo["xml"]!["prepay_id"]!["#cdata-section"]!.ToString();

                DtoCreatePayApp info = new()
                {
                    AppId = appid,
                    PartnerId = mchid,
                    PrepayId = prepay_id,
                    Package = "Sign=WXPay",
                    NonceStr = nonceStr
                };

                //再次签名返回数据至APP
                string strB = "appid=" + appid + "&noncestr=" + nonceStr + "&package=Sign=WXPay&partnerid=" + info.PartnerId + "&prepayid=" + prepay_id + "&timestamp=" + info.TimeStamp + "&key=" + mchkey;

                info.Sign = Common.CryptoHelper.GetMD5(strB).ToUpper();

                return info;
            }
            else
            {
                return null;
            }
        }



        /// <summary>
        /// 获取 AccessToken
        /// </summary>
        /// <returns></returns>
        public (string accessToken, string openId) GetAccessToken(IDistributedCache distributedCache, IHttpClientFactory httpClientFactory, string code)
        {
            string token = distributedCache.GetString("wxappaccesstoken" + code);
            string openid = distributedCache.GetString("wxappopenid" + code);

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(openid))
            {

                string url = "https://api.weixin.qq.com/sns/oauth2/access_token?appid=" + appid + "&secret=" + appsecret + "&code=" + code + "&grant_type=authorization_code";

                var returnJson = httpClientFactory.Post(url, "", "form");

                var retToken = JsonHelper.GetValueByKey(returnJson, "access_token");
                var retOpenId = JsonHelper.GetValueByKey(returnJson, "openid");

                if (retToken != null && retOpenId != null)
                {
                    token = retToken;
                    openid = retOpenId;
                }

                if (!string.IsNullOrEmpty(token))
                {
                    distributedCache.SetString("wxappaccesstoken" + code, token, TimeSpan.FromSeconds(7100));
                    distributedCache.SetString("wxappopenid" + code, openid, TimeSpan.FromSeconds(7100));
                }
            }

            return (token, openid);
        }



        /// <summary>
        /// 获取用户信息
        /// </summary>
        /// <param name="httpClientFactory"></param>
        /// <param name="accessToken"></param>
        /// <param name="openId"></param>
        /// <returns></returns>
        public DtoGetUserInfo GetUserInfo(IHttpClientFactory httpClientFactory, string accessToken, string openId)
        {
            string url = "https://api.weixin.qq.com/sns/userinfo?access_token=" + accessToken + "&openid=" + openId;

            var returnJson = httpClientFactory.Post(url, "", "form");

            var userInfo = JsonHelper.JsonToObject<DtoGetUserInfo>(returnJson);

            return userInfo;
        }
    }
}
