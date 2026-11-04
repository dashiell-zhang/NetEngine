using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace EventBus;

/// <summary>
/// 注册共享广播运行能力并接入指定后端
/// </summary>
public static class EventBusServiceCollectionExtensions
{

    /// <summary>
    /// 在业务服务注册完成后配置唯一的广播后端
    /// </summary>
    public static IServiceCollection AddEventBus<TTransport>(this IServiceCollection services, Action<EventBusOptions>? configure = null) where TTransport : class, IEventTransport
    {

        if (services.Any(t => t.ServiceType == typeof(IEventTransport)))
        {
            throw new InvalidOperationException("同一宿主只能注册一个事件后端");
        }

        services.AddOptions<EventBusOptions>()
            .Configure(options => configure?.Invoke(options))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Scope) && options.Scope.Length <= 200, "EventBus.Scope 必须为不超过 200 个字符的非空字符串")
            .Validate(options => Enum.IsDefined(options.Provider), "EventBus.Provider 无效")
            .Validate(options => options.Retention > TimeSpan.Zero && options.PollInterval > TimeSpan.Zero && options.RetryInterval > TimeSpan.Zero && options.CleanupInterval > TimeSpan.Zero, "事件保留时间和运行间隔必须大于零")
            .Validate(options => options.BatchSize is > 0 and <= 1000, "EventBus.BatchSize 必须介于 1 和 1000")
            .ValidateOnStart();

        services.AddSingleton(new EventListenerRegistry(services.ToArray()));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IEventTransport, TTransport>();
        services.AddSingleton<IEventPublisher, EventPublisher>();
        services.AddSingleton<IHostedService, EventBusHostedService>();
        return services;

    }

}
