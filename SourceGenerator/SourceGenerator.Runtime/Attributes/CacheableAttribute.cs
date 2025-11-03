using SourceGenerator.Runtime.Pipeline.Behaviors;

namespace SourceGenerator.Runtime.Attributes;

/// <summary>
/// 为标注的方法开启缓存（需要有返回值的方法生效）
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class CacheableAttribute : ProxyBehaviorAttribute<CacheableBehavior, Options.CacheableOptions>
{
    /// <summary>
    /// 缓存有效期（秒）
    /// </summary>
    public int TtlSeconds { get; set; } = 60;
}

