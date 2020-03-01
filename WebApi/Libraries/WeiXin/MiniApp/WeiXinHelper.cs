using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Xml;
using WebApi.Libraries.WeiXin.MiniApp.Models;

namespace WebApi.Libraries.WeiXin.MiniApp
{
    public class WeiXinHelper
    {
        private string appid;

        private string secret;

        private string mchid;

        private string mchkey;

        private string notifyurl;

        public WeiXinHelper(string in_appid, string in_secret, string in_mchid = null, string in_mchkey = null, string in_notifyurl = null)
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
        /// <param name="code"></param>
        /// <returns></returns>
        public (string openid, string sessionkey) GetOpenIdAndSessionKey(string code)
        {
            string url = "https://api.weixin.qq.com/sns/jscode2session?appid=" + appid + "&secret=" + secret + "&js_code=" + code + "&grant_type=authorization_code";

            string httpret = Common.Http.HttpHelper.Get(url);

            string openid = Common.Json.JsonHelper.GetValueByKey(httpret, "openid");

            string sessionkey = Common.Json.JsonHelper.GetValueByKey(httpret, "session_key");

            return (openid, sessionkey);
        }


        /// <summary>
        /// 微信小程序支付商户平台下单方法
        /// </summary>
        /// <param name="openid">用户 OpenId</param>
        /// <param name="orderno">订单号</param>
        /// <param name="title">商品名称</param>
        /// <param name="body">商品描述</param>
        /// <param name="price">价格，单位为分</param>
        /// <returns></returns>
        public CreatePay_MiniApp CreatePay(string openid, string orderno, string title, string body, int price)
        {

            string nonceStr = Guid.NewGuid().ToString().Replace("-", "");


            var url = "https://api.mch.weixin.qq.com/pay/unifiedorder";//微信统一下单请求地址


            //参与统一下单签名的参数，除最后的key外，已经按参数名ASCII码从小到大排序
            var unifiedorderSignParam = string.Format("appid={0}&body={1}&mch_id={2}&nonce_str={3}&notify_url={4}&openid={5}&out_trade_no={6}&total_fee={7}&trade_type={8}&key={9}"
                , appid, body, mchid, nonceStr, notifyurl
                , openid, orderno, price, "JSAPI", mchkey);


            var unifiedorderSign = Common.Crypto.Md5.GetMd5(unifiedorderSignParam).ToUpper();

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


            var getdata = Common.Http.HttpHelper.Post(url, zhi, "form");

            //获取xml数据
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(getdata);


            //xml格式转json
            string json = Newtonsoft.Json.JsonConvert.SerializeXmlNode(doc);
            JObject jo = (JObject)JsonConvert.DeserializeObject(json);


            if (jo["xml"]["return_code"]["#cdata-section"].ToString() == "SUCCESS")
            {
                string prepay_id = jo["xml"]["prepay_id"]["#cdata-section"].ToString();

                CreatePay_MiniApp info = new CreatePay_MiniApp();
                info.nonceStr = nonceStr;
                info.package = "prepay_id=" + prepay_id;

                //再次签名返回数据至小程序
                string strB = "appId=" + appid + "&nonceStr=" + nonceStr + "&package=prepay_id=" + prepay_id + "&signType=MD5&timeStamp=" + info.timeStamp + "&key=" + mchkey;

                info.paySign = Common.Crypto.Md5.GetMd5(strB).ToUpper();

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
            // RijndaelManaged rijalg = new RijndaelManaged();
            //----------------- 
            //设置 cipher 格式 AES-128-CBC 

            rijalg.KeySize = 128;

            rijalg.Padding = PaddingMode.PKCS7;
            rijalg.Mode = CipherMode.CBC;

            rijalg.Key = Convert.FromBase64String(key);
            rijalg.IV = Convert.FromBase64String(iv);


            byte[] encryptedData = Convert.FromBase64String(encryptedDataStr);
            //解密 
            ICryptoTransform decryptor = rijalg.CreateDecryptor(rijalg.Key, rijalg.IV);

            string result;

            using (MemoryStream msDecrypt = new MemoryStream(encryptedData))
            {
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                {
                    using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                    {

                        result = srDecrypt.ReadToEnd();
                    }
                }
            }
            return result;
        }

    }
}
