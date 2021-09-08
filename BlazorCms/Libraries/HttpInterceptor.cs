using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorCms.Libraries
{


    /// <summary>
    /// Http请求拦截器
    /// </summary>
    public class HttpInterceptor : DelegatingHandler
    {

        private readonly ISyncLocalStorageService LocalStorage;
        private readonly NavigationManager NavigationManager;

        public HttpInterceptor(ISyncLocalStorageService _LocalStorage, NavigationManager _NavigationManager)
        {
            LocalStorage = _LocalStorage;
            NavigationManager = _NavigationManager;
            InnerHandler = new HttpClientHandler();
        }


        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var authorization = LocalStorage.GetItem<string>("Authorization");

            if (!string.IsNullOrEmpty(authorization))
            {
                request.Headers.Add("Authorization", "Bearer " + authorization);
            }

            var response = await base.SendAsync(request, cancellationToken);

            if ((int)response.StatusCode == 401)
            {
                NavigationManager.NavigateTo("/login");
            }

            return response;
        }
    }
}
