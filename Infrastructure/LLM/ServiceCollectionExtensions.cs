using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LLM;

/// <summary>
/// LLM 基础服务的依赖注入扩展
/// </summary>
public static class ServiceCollectionExtensions
{

    /// <summary>
    /// 注册 LLM 客户端工厂
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddLlmClientFactory(this IServiceCollection services)
    {
        services.TryAddSingleton<ILlmClientFactory, LlmClientFactory>();
        return services;
    }

}
