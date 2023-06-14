using Common;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace WebAPI.Libraries.WeiXin.MiniApp
{
    public class WeiXinHelper
    {
        private readonly string appid;

        private readonly string secret;

        private readonly string? mchid;


        public WeiXinHelper(string in_appid, string in_secret, string? in_mchid = null)
        {
            appid = in_appid;
            secret = in_secret;
            mchid = in_mchid;
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

            string httpret = httpClient.GetAsync(url).Result.Content.ReadAsStringAsync().Result;

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


    }
}
