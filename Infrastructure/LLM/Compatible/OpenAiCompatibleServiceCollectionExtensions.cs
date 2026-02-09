using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace LLM.Compatible;

public static class OpenAiCompatibleServiceCollectionExtensions
{
    /// <summary>
    /// 注册一个 OpenAI-Compatible LLM Provider（通过 providerKey 进行 keyed 解析）
    /// </summary>
    public static IServiceCollection AddOpenAiCompatibleProvider(
        this IServiceCollection services,
        string providerKey,
        Action<OpenAiCompatibleProviderSetting> configure)
    {
        services.AddLlmClientFactory();

        services.AddOptions<OpenAiCompatibleProviderSetting>(providerKey).Configure(configure);

        services.AddHttpClient(providerKey, (serviceProvider, httpClient) =>
        {
            var settings = serviceProvider
                .GetRequiredService<IOptionsMonitor<OpenAiCompatibleProviderSetting>>()
                .Get(providerKey);

            httpClient.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
            httpClient.Timeout = TimeSpan.FromSeconds(60);

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

        services.AddKeyedTransient<ILlmClient>(providerKey, (serviceProvider, _) =>
        {
            var settings = serviceProvider
                .GetRequiredService<IOptionsMonitor<OpenAiCompatibleProviderSetting>>()
                .Get(providerKey);

            var httpClient = serviceProvider
                .GetRequiredService<IHttpClientFactory>()
                .CreateClient(providerKey);

            return new OpenAiCompatibleProviderClient(httpClient, settings, providerKey);
        });

        return services;
    }
}
