using Microsoft.Extensions.Logging;

namespace SourceGenerator.Runtime.Pipeline;

/// <summary>
/// 表示一次代理调用在行为管道中的上下文信息
/// </summary>
public sealed class InvocationContext
{

    /// <summary>
    /// 方法的标识名称 一般为类型名加方法名
    /// </summary>
    public required string Method { get; init; }


    /// <summary>
    /// 方法调用的参数快照 一般为可序列化对象
    /// </summary>
    public object? Args { get; init; }


    /// <summary>
    /// 当前调用的追踪标识
    /// </summary>
    public required Guid TraceId { get; init; }


    /// <summary>
    /// 是否启用日志记录
    /// </summary>
    public bool Log { get; init; }


    /// <summary>
    /// 是否存在返回值
    /// </summary>
    public bool HasReturnValue { get; init; }


    /// <summary>
    /// 是否允许将返回值序列化到日志中
    /// </summary>
    public bool AllowReturnSerialization { get; init; }


    /// <summary>
    /// 当前调用可用的服务提供程序 用于在行为中解析依赖
    /// </summary>
    public IServiceProvider? ServiceProvider { get; init; }


    /// <summary>
    /// 日志记录器实例
    /// </summary>
    public ILogger? Logger { get; init; }


    /// <summary>
    /// 异步调用管道中注册的行为列表
    /// </summary>
    public IReadOnlyList<IInvocationAsyncBehavior>? Behaviors { get; init; }


    // 用于行为之间传递特定数据或配置的特性存储容器
    public Dictionary<Type, object> Features { get; } = new();


    /// <summary>
    /// 从特性存储容器中获取指定类型的特性对象
    /// </summary>
    /// <typeparam name="T">特性类型</typeparam>
    /// <returns>对应类型的特性实例或 null</returns>
    public T? GetFeature<T>() where T : class
        => Features.TryGetValue(typeof(T), out var value) ? (T)value : null;


    /// <summary>
    /// 向特性存储容器中设置指定类型的特性对象
    /// </summary>
    /// <typeparam name="T">特性类型</typeparam>
    /// <param name="feature">待存储的特性实例</param>
    public void SetFeature<T>(T feature) where T : class
    {
        if (feature is null) return;
        Features[typeof(T)] = feature;
    }

}
