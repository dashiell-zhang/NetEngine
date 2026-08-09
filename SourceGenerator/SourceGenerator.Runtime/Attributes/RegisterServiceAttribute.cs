using Microsoft.Extensions.DependencyInjection;

namespace SourceGenerator.Runtime.Attributes;

/// <summary>
/// 标记需要自动生成 DI 注册代码的服务类型
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class RegisterServiceAttribute : Attribute
{

    /// <summary>
    /// 创建使用自动服务类型选择规则的注册特性
    /// </summary>
    public RegisterServiceAttribute()
    {

    }


    /// <summary>
    /// 创建显式指定服务类型的注册特性
    /// </summary>
    /// <param name="serviceType">需要注册到依赖注入容器的服务类型</param>
    public RegisterServiceAttribute(Type serviceType)
    {

        ServiceType = serviceType;

    }


    /// <summary>
    /// 显式指定的服务类型，为 null 时使用自动选择规则
    /// </summary>
    public Type? ServiceType { get; }


    /// <summary>
    /// 生命周期，默认 Transient
    /// </summary>
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;


    /// <summary>
    /// 可选的 Key，用于 Keyed Service
    /// </summary>
    public object? Key { get; set; }

}
