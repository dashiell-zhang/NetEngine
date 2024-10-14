using Admin.App.Libraries;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Globalization;

namespace Admin.App
{
    public class Program
    {
        public static async Task Main(string[] args)
        {

            var builder = WebAssemblyHostBuilder.CreateDefault(args);


            var appApiUrl = "https://localhost:9833/";
            //var appApiUrl = builder.HostEnvironment.BaseAddress.ToLower();



            CultureInfo.DefaultThreadCurrentCulture = new("zh-CN");
            CultureInfo.DefaultThreadCurrentUICulture = new("zh-CN");

            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            builder.Services.AddTransient<HttpInterceptor>();

            builder.Services.AddScoped(sp => new HttpClient(sp.GetRequiredService<HttpInterceptor>())
            {
                BaseAddress = new Uri(appApiUrl)
            });

            builder.Services.AddBlazoredLocalStorage();

            builder.Services.AddAntDesign();

            await using WebAssemblyHost host = builder.Build();

            var localStorage = host.Services.GetRequiredService<ISyncLocalStorageService>();
            localStorage.SetItemAsString("appApiUrl", appApiUrl);

            await host.RunAsync();
        }
    }
}
