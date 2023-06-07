using Common;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using WebAPI.Libraries.WeiXin.MiniApp.Models;

namespace WebAPI.Libraries.WeiXin.MiniApp
{
    public class WeiXinHelper
    {
        private readonly string appid;

        private readonly string secret;

        private readonly string? mchid;

        private readonly string? mchkey;

        private readonly string? notifyurl;

        public WeiXinHelper(string in_appid, string in_secret, string? in_mchid = null, string? in_mchkey = null, string? in_notifyurl = null)
        {
            appid = in_appid;
            secret = in_secret;
            mchid = in_mchid;
            mchkey = in_mchkey;
            notifyurl = in_notifyurl;
        }



        /// <summary>
        /// 获取用户OpenId 和 SessionKey
        /// </summary>
        /// <param name="distributedCache"></param>
        /// <param name="httpClient"></param>
        /// <param name="code"></param>
        /// <returns></returns>
        public (string openid, string sessionkey) GetOpenIdAndSessionKey(IDistributedCache distributedCache, HttpClient httpClient, string code)
        {
            string url = "https://api.weixin.qq.com/sns/jscode2session?appid=" + appid + "&secret=" + secret + "&js_code=" + code + "&grant_type=authorization_code";

            string httpret = httpClient.Get(url);

            try
            {
                string openid = JsonHelper.GetValueByKey(httpret, "openid")!;
                string sessionkey = JsonHelper.GetValueByKey(httpret, "session_key")!;

                distributedCache.Set(code, httpret, new TimeSpan(0, 0, 10));

                return (openid, sessionkey);
            }
            catch
            {
                string errcode = JsonHelper.GetValueByKey(httpret, "errcode")!;

                if (errcode == "40163")
                {
                    var cachHttpRet = distributedCache.GetString(code);

                    if (!string.IsNullOrEmpty(cachHttpRet))
                    {
                        httpret = cachHttpRet;
                    }
                }

                string openid = JsonHelper.GetValueByKey(httpret, "openid")!;
                string sessionkey = JsonHelper.GetValueByKey(httpret, "session_key")!;

                return (openid, sessionkey);
            }
        }


        /// <summary>
        /// 微信小程序支付商户平台下单方法
        /// </summary>
        /// <param name="httpClient"></param>
        /// <param name="openid">用户 OpenId</param>
        /// <param name="orderno">订单号</param>
        /// <param name="body">商品描述</param>
        /// <param name="price">价格，单位为分</param>
        /// <returns></returns>
        public DtoCreatePayMiniApp? CreatePay(HttpClient httpClient, string openid, string orderno, string body, int price)
        {

            string nonceStr = Guid.NewGuid().ToString().Replace("-", "");

            var url = "https://api.mch.weixin.qq.com/pay/unifiedorder";//微信统一下单请求地址

            //参与统一下单签名的参数，除最后的key外，已经按参数名ASCII码从小到大排序
            var unifiedorderSignParam = string.Format("appid={0}&body={1}&mch_id={2}&nonce_str={3}&notify_url={4}&openid={5}&out_trade_no={6}&total_fee={7}&trade_type={8}&key={9}"
                , appid, body, mchid, nonceStr, notifyurl
                , openid, orderno, price, "JSAPI", mchkey);

            var unifiedorderSign = Common.CryptoHelper.GetMD5(unifiedorderSignParam).ToUpper();

            //构造统一下单的请求参数
            var zhi = string.Format(@"<xml>
                                <appid>{0}</appid>                                              
                                <body>{1}</body>
                                <mch_id>{2}</mch_id>   
                                <nonce_str>{3}</nonce_str>
                                <notify_url>{4}</notify_url>
                                <openid>{5}</openid>
                                <out_trade_no>{6}</out_trade_no>
                                <total_fee>{7}</total_fee>
                                <trade_type>{8}</trade_type>
                                <sign>{9}</sign>
                               </xml>
                    ", appid, body, mchid, nonceStr, notifyurl, openid
                              , orderno, price, "JSAPI", unifiedorderSign);


            var getdata = httpClient.Post(url, zhi, "form");

            //获取xml数据
            XmlDocument doc = new();
            doc.LoadXml(getdata);


            //xml格式转json
            string json = JsonConvert.SerializeXmlNode(doc);
            JObject jo = (JObject)JsonConvert.DeserializeObject(json)!;


            if (jo["xml"]!["return_code"]!["#cdata-section"]!.ToString() == "SUCCESS")
            {
                string prepay_id = jo["xml"]!["prepay_id"]!["#cdata-section"]!.ToString();

                DtoCreatePayMiniApp info = new()
                {
                    NonceStr = nonceStr,
                    Package = "prepay_id=" + prepay_id
                };

                //再次签名返回数据至小程序
                string strB = "appId=" + appid + "&nonceStr=" + nonceStr + "&package=prepay_id=" + prepay_id + "&signType=MD5&timeStamp=" + info.TimeStamp + "&key=" + mchkey;

                info.PaySign = Common.CryptoHelper.GetMD5(strB).ToUpper();

                return info;
            }
            else
            {
                return null;
            }
        }



        /// <summary>
        /// 微信小程序 encryptedData 解密
        /// </summary>
        /// <param name="encryptedDataStr">encryptedDataStr</param>
        /// <param name="key">session_key</param>
        /// <param name="iv">iv</param>
        /// <returns></returns>
        public static string DecryptionData(string encryptedDataStr, string key, string iv)
        {
            var rijalg = Aes.Create();

            rijalg.KeySize = 128;

            rijalg.Padding = PaddingMode.PKCS7;
            rijalg.Mode = CipherMode.CBC;

            rijalg.Key = Convert.FromBase64String(key);
            rijalg.IV = Convert.FromBase64String(iv);


            byte[] encryptedData = Convert.FromBase64String(encryptedDataStr);
            //解密 
            ICryptoTransform decryptor = rijalg.CreateDecryptor(rijalg.Key, rijalg.IV);

            string result;

            using (MemoryStream msDecrypt = new(encryptedData))
            {
                using CryptoStream csDecrypt = new(msDecrypt, decryptor, CryptoStreamMode.Read);
                using StreamReader srDecrypt = new(csDecrypt);

                result = srDecrypt.ReadToEnd();
            }
            return result;
        }



        /// <summary>
        /// 证书双向校验POST
        /// </summary>
        /// <param name="url"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private string UseCretPost(string url, string data)
        {
            var sslPath = Path.Combine(Directory.GetCurrentDirectory(), "ssl", "apiclient_cert.p12");

            using HttpClientHandler handler = new();
            X509Certificate2 cert = new(sslPath, mchid, X509KeyStorageFlags.MachineKeySet);

            handler.ClientCertificates.Add(cert);

            using HttpClient client = new(handler);

            client.DefaultRequestVersion = new("2.0");

            using Stream dataStream = new MemoryStream(Encoding.UTF8.GetBytes(data));
            using HttpContent content = new StreamContent(dataStream);

            content.Headers.ContentType = new("application/x-www-form-urlencoded");

            using var httpResponse = client.PostAsync(url, content);
            return httpResponse.Result.Content.ReadAsStringAsync().Result;

        }



    }
}
