using Common;
using Microsoft.Extensions.Caching.Distributed;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Client.WebAPI.Libraries.HttpHandler;


/// <summary>
/// Http签名处理模块
/// </summary>
public class HttpSignHandler(IDistributedCache distributedCache, IHttpClientFactory httpClientFactory) : DelegatingHandler
{
    private readonly HttpClient httpClient = httpClientFactory.CreateClient();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var authorization = await GetTokenAsync(cancellationToken);


        var timeStr = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var privateKey = authorization.Split(".").ToList().LastOrDefault();
        var requestUrl = request.RequestUri?.PathAndQuery;

        var dataStr = privateKey + timeStr + requestUrl;

        if (request.Content != null && request.Content.Headers.ContentType != null)
        {
            if (request.Content.Headers.ContentType.MediaType == "application/json")
            {
                var requestBody = await request.Content.ReadAsStringAsync(cancellationToken);

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
                    var value = await item.ReadAsStringAsync(cancellationToken);
                    dataStr = dataStr + item.Headers.ContentDisposition?.Name + value;
                }

                foreach (var item in dataContents!.Where(t => t.Headers.ContentType == null).OrderBy(t => t.Headers.ContentDisposition?.Name).ToList())
                {
                    using SHA256 sha256 = SHA256.Create();
                    var fileStream = await item.ReadAsStreamAsync(cancellationToken);

                    if (!fileStream.CanSeek)
                    {
                        throw new InvalidOperationException("multipart/form-data 文件流不支持 Seek，HttpSignHandler 无法在读取签名后复位流位置，请改为使用可 Seek 的流（如 FileStream/MemoryStream）");
                    }

                    var fileSign = Convert.ToHexString(sha256.ComputeHash(fileStream));

                    fileStream.Position = 0;

                    dataStr = dataStr + item.Headers.ContentDisposition?.Name + fileSign;
                }
            }
        }
        string dataSign = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dataStr)));

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authorization);
        request.Headers.Add("Token", dataSign);
        request.Headers.Add("Time", timeStr);


        var response = await base.SendAsync(request, cancellationToken);

        if ((int)response.StatusCode == 200 && response.Headers.Contains("NewToken"))
        {
            var newToken = response.Headers.GetValues("NewToken").ToList().FirstOrDefault();

            if (!string.IsNullOrEmpty(newToken))
            {
                await distributedCache.SetStringAsync("token", newToken, cancellationToken);
            }
        }

        return response;

    }


    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        var token = await distributedCache.GetStringAsync("token", cancellationToken);

        if (string.IsNullOrEmpty(token))
        {
            var getTK = new
            {
                name = "admin",
                passWord = "123456"
            };

            var getTKStr = JsonHelper.ObjectToJson(getTK);

            using var content = new StringContent(getTKStr, Encoding.UTF8, "application/json");
            using var httpResponseMessage = await httpClient.PostAsync("https://localhost:9833/api/Authorize/GetToken", getTKStr, "json", cancellationToken: cancellationToken);

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                token = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken);
                await distributedCache.SetStringAsync("token", token, cancellationToken);
            }
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new Exception("token获取失败");
        }

        return token;
    }


}
