using Common;
using DistributedLock;
using Microsoft.Extensions.Caching.Distributed;
using Repository.Database;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using WebAPI.Models.Pay;

namespace WebAPI.Services
{
    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class PayService(IHttpClientFactory httpClientFactory, DatabaseContext db, IDistributedCache distributedCache, IDistributedLock distributedLock)
    {
        private readonly HttpClient httpClient = httpClientFactory.CreateClient();



        /// <summary>
        /// 微信支付Http请求
        /// </summary>
        /// <param name="mchId">商户Id</param>
        /// <param name="url">接口地址</param>
        /// <param name="data">请求数据，数据为空则认为是get请求</param>
        /// <returns></returns>
        public string WeiXinPayHttp(string mchId, string url, object? data = null)
        {
            var weiXinPayGroupId = db.TAppSetting.Where(t => t.Module == "WeiXinPay" && t.Key == "MchId" && t.Value == mchId).Select(t => t.GroupId).FirstOrDefault();

            var settings = db.TAppSetting.Where(t => t.Module == "WeiXinPay" && t.GroupId == weiXinPayGroupId).ToList();

            string mchApiCertId = settings.Where(t => t.Key == "MchApiCertId").Select(t => t.Value).First();
            string mchApiCertKey = settings.Where(t => t.Key == "MchApiCertKey").Select(t => t.Value).First();
            string mchApiV3Key = settings.Where(t => t.Key == "MchApiV3Key").Select(t => t.Value).First();

            string dataJson = data == null ? "" : JsonHelper.ObjectToJson(data);

            string method = data == null ? "GET" : "POST";

            long timeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            string nonceStr = Path.GetRandomFileName();
            string message = $"{method}\n{url[29..]}\n{timeStamp}\n{nonceStr}\n{dataJson}\n";
            string signature = CryptoHelper.RSASignData(mchApiCertKey, message, "base64", HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            string authorization = $"WECHATPAY2-SHA256-RSA2048 mchid=\"{mchId}\",nonce_str=\"{nonceStr}\",timestamp=\"{timeStamp}\",serial_no=\"{mchApiCertId}\",signature=\"{signature}\"";


            HttpRequestMessage requestMessage = new()
            {
                Method = data == null ? HttpMethod.Get : HttpMethod.Post,
                RequestUri = new Uri(url),
                Version = httpClient.DefaultRequestVersion,
                VersionPolicy = httpClient.DefaultVersionPolicy
            };


            if (method == "POST")
            {
                requestMessage.Content = new StringContent(dataJson);
                requestMessage.Content.Headers.ContentType = new("application/json")
                {
                    CharSet = "utf-8"
                };

            }

            requestMessage.Headers.Add("Accept", "*/*");
            requestMessage.Headers.Add("User-Agent", ".NET HttpClient");
            requestMessage.Headers.Add("Authorization", authorization);


            using var responseMessage = httpClient.SendAsync(requestMessage).Result;

            string responseBody = responseMessage.Content.ReadAsStringAsync().Result;


            DtoWeiXinPayCertificates weiXinPayCertificates = new();

            if (url == "https://api.mch.weixin.qq.com/v3/certificates")
            {
                weiXinPayCertificates = JsonHelper.JsonToObject<DtoWeiXinPayCertificates>(responseBody);

                foreach (var weiXinPayCert in weiXinPayCertificates.data)
                {
                    weiXinPayCert.certificate = CryptoHelper.AesGcmDecrypt(weiXinPayCert.encrypt_certificate.ciphertext, mchApiV3Key, weiXinPayCert.encrypt_certificate.nonce, weiXinPayCert.encrypt_certificate.associated_data, "base64");
                }
            }
            else
            {
                weiXinPayCertificates = GetWeiXinPayCertificates(mchId);
            }


            var headers = responseMessage.Headers.ToDictionary(t => t.Key, t => t.Value.First());

            if (VerifySign(headers, responseBody, weiXinPayCertificates))
            {
                if (url == "https://api.mch.weixin.qq.com/v3/certificates")
                {
                    distributedCache.Set(mchId + "GetWeiXinPayCertificates", weiXinPayCertificates, TimeSpan.FromHours(1));
                }

                return responseBody;
            }
            else
            {
                throw new Exception("签名验证异常");
            }

        }



        public DtoWeiXinPayCertificates GetWeiXinPayCertificates(string mchId)
        {
            var cacheKey = mchId + "GetWeiXinPayCertificates";

            var weiXinPayCertificates = distributedCache.Get<DtoWeiXinPayCertificates>(cacheKey);

            if (weiXinPayCertificates != null)
            {
                return weiXinPayCertificates;
            }
            else
            {
                using (distributedLock.Lock(mchId + "GetWeiXinPayCertificates" + "lock"))
                {
                    weiXinPayCertificates = distributedCache.Get<DtoWeiXinPayCertificates>(cacheKey);

                    if (weiXinPayCertificates != null)
                    {

                        return weiXinPayCertificates;
                    }
                    else
                    {
                        var certificatesRetData = WeiXinPayHttp(mchId, "https://api.mch.weixin.qq.com/v3/certificates");

                        if (certificatesRetData != null)
                        {
                            weiXinPayCertificates = distributedCache.Get<DtoWeiXinPayCertificates>(cacheKey);
                        }

                        if (weiXinPayCertificates != null)
                        {
                            return weiXinPayCertificates;
                        }
                        else
                        {
                            throw new Exception("证书获取失败");
                        }
                    }
                }
            }
        }



        public bool VerifySign(Dictionary<string, string> headers, string body, DtoWeiXinPayCertificates weiXinPayCertificates)
        {
            string wechatPayNonce = headers.First(t => t.Key == "Wechatpay-Nonce").Value.ToString();
            string wechatpaySignature = headers.First(t => t.Key == "Wechatpay-Signature").Value.ToString();
            string wechatpaySerial = headers.First(t => t.Key == "Wechatpay-Serial").Value.ToString();
            string wechatpayTimestamp = headers.First(t => t.Key == "Wechatpay-Timestamp").Value.ToString();

            var certificate = weiXinPayCertificates.data.Where(t => t.serial_no == wechatpaySerial).Select(t => t.certificate).First();

            if (certificate != null)
            {
                string message = $"{wechatpayTimestamp}\n{wechatPayNonce}\n{body}\n";

                using X509Certificate2 x509Certificate2 = new(Encoding.UTF8.GetBytes(certificate));
                using var rsa = x509Certificate2.GetRSAPublicKey();

                if (rsa != null)
                {
                    var publicKey = rsa.ExportRSAPublicKeyPem();

                    return CryptoHelper.RSAVerifyData(publicKey, message, wechatpaySignature, "base64", HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                }
            }

            return false;
        }

    }
}
