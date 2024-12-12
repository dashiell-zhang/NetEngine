using Common;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Cryptography;
using System.Text;

namespace Client.WebAPI.Libraries.HttpHandler
{


    /// <summary>
    /// Http签名处理模块
    /// </summary>
    public class HttpSignHandler(IDistributedCache distributedCache, IHttpClientFactory httpClientFactory) : DelegatingHandler
    {
        private readonly HttpClient httpClient = httpClientFactory.CreateClient();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var authorization = GetToken();

            var timeStr = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            var privateKey = authorization.Split(".").ToList().LastOrDefault();
            var requestUrl = request.RequestUri?.PathAndQuery;

            var dataStr = privateKey + timeStr + requestUrl;

            if (request.Content != null && request.Content.Headers.ContentType != null)
            {
                if (request.Content.Headers.ContentType.MediaType == "application/json")
                {
                    var requestBody = request.Content?.ReadAsStringAsync(cancellationToken).Result;

                    if (requestBody != null)
                    {
                        dataStr += requestBody;
                    }
                }
                else if (request.Content.Headers.ContentType.MediaType == "multipart/form-data")
                {
                    var dataContents = request.Content as MultipartFormDataContent;

                    foreach (var item in dataContents!.Where(t => t.Headers.ContentType?.MediaType == "text/plain").OrderBy(t => t.Headers.ContentDisposition?.Name).ToList())
                    {
                        dataStr = dataStr + item.Headers.ContentDisposition?.Name + item.ReadAsStringAsync(cancellationToken).Result;
                    }

                    foreach (var item in dataContents!.Where(t => t.Headers.ContentType == null).OrderBy(t => t.Headers.ContentDisposition?.Name).ToList())
                    {
                        using SHA256 sha256 = SHA256.Create();
                        var fileSign = Convert.ToHexString(sha256.ComputeHash(item.ReadAsStream(cancellationToken)));

                        item.ReadAsStream(cancellationToken).Position = 0;

                        dataStr = dataStr + item.Headers.ContentDisposition?.Name + fileSign;
                    }
                }
            }
            string dataSign = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dataStr)));

            request.Headers.Add("Authorization", "Bearer " + authorization);
            request.Headers.Add("Token", dataSign);
            request.Headers.Add("Time", timeStr);


            var response = await base.SendAsync(request, cancellationToken);

            if ((int)response.StatusCode == 200 && response.Headers.Contains("NewToken"))
            {
                var newToken = response.Headers.GetValues("NewToken").ToList().FirstOrDefault();

                if (!string.IsNullOrEmpty(newToken))
                {
                    distributedCache.SetString("token", newToken);
                }
            }

            return response;

        }


        private string GetToken()
        {
            var token = distributedCache.GetString("token");

            if (string.IsNullOrEmpty(token))
            {
                var getTK = new
                {
                    name = "admin",
                    passWord = "123456"
                };

                var getTKStr = JsonHelper.ObjectToJson(getTK);

                token = httpClient.PostAsync("https://localhost:9833/api/Authorize/GetToken", getTKStr, "json").Result.Content.ReadAsStringAsync().Result;

                distributedCache.SetString("token", token);
            }

            return token;
        }


    }
}
