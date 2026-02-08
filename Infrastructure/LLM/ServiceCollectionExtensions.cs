using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LLM;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLlmClientFactory(this IServiceCollection services)
    {
        services.TryAddSingleton<ILlmClientFactory, LlmClientFactory>();
        return services;
    }
}

