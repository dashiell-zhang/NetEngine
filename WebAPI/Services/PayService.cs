using Common;
using Repository.Database;
using System.Text;

namespace WebAPI.Services
{
    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class PayService
    {
        private readonly HttpClient httpClient;
        private readonly DatabaseContext db;


        public PayService(IHttpClientFactory httpClientFactory, DatabaseContext db)
        {
            httpClient = httpClientFactory.CreateClient("");
            this.db = db;
        }



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

            string dataJson = data == null ? "" : JsonHelper.ObjectToJson(data);

            string method = data == null ? "GET" : "POST";

            long timeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            string nonceStr = Path.GetRandomFileName();
            string message = $"{method}\n{url[29..]}\n{timeStamp}\n{nonceStr}\n{dataJson}\n";
            string signature = CryptoHelper.GetSHA256withRSA(message, mchApiCertKey, "base64");
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
                requestMessage.Content.Headers.ContentType = new("application/json");
                requestMessage.Content.Headers.ContentType.CharSet = "utf-8";

            }

            requestMessage.Headers.Add("Accept", "*/*");
            requestMessage.Headers.Add("User-Agent", ".NET HttpClient");
            requestMessage.Headers.Add("Authorization", authorization);


            using var responseMessage = httpClient.SendAsync(requestMessage).Result;


            string responseBody = responseMessage.Content.ReadAsStringAsync().Result;

            string wechatPayNonce = responseMessage.Headers.GetValues("Wechatpay-Nonce").First();
            string wechatpaySignature = responseMessage.Headers.GetValues("Wechatpay-Signature").First();
            string wechatpayTimestamp = responseMessage.Headers.GetValues("Wechatpay-Timestamp").First();

            message = $"{wechatpayTimestamp}\n{wechatPayNonce}\n{responseBody}\n";


            return responseBody;
        }





    }
}
