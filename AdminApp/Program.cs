using AdminApp.Libraries;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;

namespace AdminApp
{
    public class Program
    {
        public static async Task Main(string[] args)
        {

            var builder = WebAssemblyHostBuilder.CreateDefault(args);


            var appApiUrl = "https://localhost:9833/api/";
            //var appApiUrl = builder.HostEnvironment.BaseAddress.ToLower();



            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("zh-CN");
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("zh-CN");

            builder.RootComponents.Add<App>("#app");

            builder.Services.AddScoped<HttpInterceptor>();



            builder.Services.AddScoped(sp => new HttpClient(sp.GetRequiredService<HttpInterceptor>())
            {
                BaseAddress = new Uri(appApiUrl)
            });


            builder.Services.AddBlazoredLocalStorage();

            builder.Services.AddAntDesign();

            await using WebAssemblyHost host = builder.Build();


            var localStorage = host.Services.GetService<ISyncLocalStorageService>();
            localStorage.SetItemAsString("AppApiUrl", appApiUrl);

            await host.RunAsync();

        }
    }
}
