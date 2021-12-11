using AntDesign;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using System.Linq;
using System.Net.Http;
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
            var authorization = LocalStorage.GetItem<string>("Authorization");

            var isGetToken = request.RequestUri.AbsolutePath.Contains("/api/Authorize/GetToken", System.StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(authorization) || isGetToken)
            {

                if (!isGetToken)
                {
                    request.Headers.Add("Authorization", "Bearer " + authorization);
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

                if ((int)response.StatusCode == 200)
                {
                    if (response.Headers.Contains("NewToken"))
                    {
                        var newToken = response.Headers.GetValues("NewToken").ToList().FirstOrDefault();

                        if (!string.IsNullOrEmpty(newToken))
                        {
                            LocalStorage.SetItem<string>("Authorization", newToken);
                        }
                    }
                }

                return response;
            }
            else
            {
                NavigationManager.NavigateTo("login", true);
                return default;
            }
        }
    }
}
