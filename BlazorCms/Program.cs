using BlazorCms.Libraries;
using BlazorCms.Libraries.JsonConverter;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlazorCms
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            builder.RootComponents.Add<App>("#app");

            builder.Services.AddScoped<HttpInterceptor>();

            builder.Services.AddScoped(sp => new HttpClient(sp.GetRequiredService<HttpInterceptor>())
            {
                BaseAddress = new Uri("https://localhost:9561/api/")
            });


            builder.Services.AddBlazoredLocalStorage();

            builder.Services.AddAntDesign();


            await builder.Build().RunAsync();
        }


    }
}
