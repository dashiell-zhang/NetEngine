using LLM.Qwen.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace LLM.Qwen;

public static class ServiceCollectionExtensions
{
    public static void AddQwenLLM(this IServiceCollection services, Action<QwenSetting> action)
    {
        services.Configure(action);

        services.AddHttpClient<QwenLlmClient>((serviceProvider, httpClient) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<QwenSetting>>().Value;

            httpClient.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
            httpClient.Timeout = settings.Timeout;

            if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
            }
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            UseCookies = false
        });

        services.AddTransient<ILlmClient>(serviceProvider => serviceProvider.GetRequiredService<QwenLlmClient>());
    }
}

