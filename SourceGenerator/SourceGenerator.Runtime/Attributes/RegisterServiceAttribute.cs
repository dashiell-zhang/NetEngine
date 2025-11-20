using Microsoft.Extensions.DependencyInjection;

namespace SourceGenerator.Runtime.Attributes;

/// <summary>
/// 标记需要自动生成 DI 注册代码的服务类型
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class RegisterServiceAttribute : Attribute
{

    /// <summary>
    /// 生命周期，默认 Transient。
    /// </summary>
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;


    /// <summary>
    /// 可选的 Key，用于 Keyed Service
    /// </summary>
    public object? Key { get; set; }

}

