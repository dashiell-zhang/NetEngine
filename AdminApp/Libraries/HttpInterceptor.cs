using AntDesign;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AdminApp.Libraries
{


    /// <summary>
    /// Http请求拦截器
    /// </summary>
    public class HttpInterceptor : DelegatingHandler
    {

        private readonly ISyncLocalStorageService LocalStorage;
        private readonly NavigationManager NavigationManager;
        private readonly NotificationService Notice;

        public HttpInterceptor(ISyncLocalStorageService _LocalStorage, NavigationManager _NavigationManager, NotificationService _Notice)
        {
            LocalStorage = _LocalStorage;
            NavigationManager = _NavigationManager;
            Notice = _Notice;
            InnerHandler = new HttpClientHandler();
        }


        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var authorization = LocalStorage.GetItemAsString("Authorization");

            var isGetToken = request.RequestUri!.AbsolutePath.Contains("/api/Authorize/GetToken", System.StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(authorization) || isGetToken)
            {
                if (!isGetToken)
                {
                    var timeStr = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                    var privateKey = authorization.Split(".").ToList().LastOrDefault();
                    var requestUrl = request.RequestUri.PathAndQuery;

                    var dataStr = privateKey + timeStr + requestUrl;

                    var requestBody = request.Content?.ReadAsStringAsync().Result;

                    if (requestBody != null)
                    {
                        dataStr = dataStr + requestBody;
                    }

                    using SHA256 sha256 = SHA256.Create();
                    string dataSign = Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(dataStr)));

                    request.Headers.Add("Authorization", "Bearer " + authorization);
                    request.Headers.Add("Token", dataSign);
                    request.Headers.Add("Time", timeStr);
                }

                var response = await base.SendAsync(request, cancellationToken);

                if ((int)response.StatusCode == 401)
                {
                    NavigationManager.NavigateTo("login");
                }

                if ((int)response.StatusCode == 400)
                {
                    var ret = await response.Content.ReadAsStringAsync(cancellationToken);
                    Notice.Open(new NotificationConfig()
                    {
                        Message = "异常",
                        Description = Json.JsonHelper.GetValueByKey(ret, "errMsg"),
                        NotificationType = NotificationType.Warning
                    });
                }

                if ((int)response.StatusCode == 200 && response.Headers.Contains("NewToken"))
                {
                    var newToken = response.Headers.GetValues("NewToken").ToList().FirstOrDefault();

                    if (!string.IsNullOrEmpty(newToken))
                    {
                        LocalStorage.SetItemAsString("Authorization", newToken);
                    }
                }

                return response;
            }
            else
            {
                NavigationManager.NavigateTo("login", true);
                return new HttpResponseMessage();
            }
        }
    }
}
