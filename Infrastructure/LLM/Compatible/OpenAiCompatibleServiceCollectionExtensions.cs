using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace LLM.Compatible;

/// <summary>
/// OpenAI-Compatible Provider 的依赖注入扩展
/// </summary>
public static class OpenAiCompatibleServiceCollectionExtensions
{

    /// <summary>
    /// 注册一个 OpenAI-Compatible LLM Provider 通过 providerKey 进行 keyed 解析和路由
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="providerKey">Provider 的 keyed 标识</param>
    /// <param name="configure">Provider 配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddOpenAiCompatibleProvider(
        this IServiceCollection services,
        string providerKey,
        Action<OpenAiCompatibleProviderSetting> configure)
    {
        services.AddLlmClientFactory();

        // 以 providerKey 作为命名 Options 存储 Provider 配置
        services.AddOptions<OpenAiCompatibleProviderSetting>(providerKey).Configure(configure);

        services.AddHttpClient(providerKey, (serviceProvider, httpClient) =>
        {
            var settings = serviceProvider
                .GetRequiredService<IOptionsMonitor<OpenAiCompatibleProviderSetting>>()
                .Get(providerKey);

            // 统一确保 BaseAddress 以 / 结尾 方便拼接相对路径
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

        // 将 ILlmClient 按 providerKey 注册为 keyed 服务
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
