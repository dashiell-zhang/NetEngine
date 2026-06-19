using Admin.App.Libraries;
using DistributedLock.InMemory;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using NetEngine.Generated;
using System.Globalization;

namespace Admin.App;

/// <summary>
/// 管理端应用入口
/// </summary>
public class Program
{

    /// <summary>
    /// 启动管理端应用
    /// </summary>
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

        builder.Services.AddHttpClient("upload", client =>
        {
            client.BaseAddress = new Uri(appApiUrl);
        });

        builder.Services.AddScoped<IBrowserLocalStorageService, BrowserLocalStorageService>();

        builder.Services.AddAntDesign();

        // 为 SourceGenerator.Runtime 的 CacheableBehavior / ConcurrencyLimitBehavior 提供 WASM 进程内实现
        // 注意：仅对当前浏览器进程生效，无法跨实例/跨机器共享
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddInMemoryLock();

        builder.Services.BatchRegisterServices();


        await using WebAssemblyHost host = builder.Build();

        var localStorage = host.Services.GetRequiredService<IBrowserLocalStorageService>();
        localStorage.SetItemAsString("appApiUrl", appApiUrl);

        await host.RunAsync();
    }
}
