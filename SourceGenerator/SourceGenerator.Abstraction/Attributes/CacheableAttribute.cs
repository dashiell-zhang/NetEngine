namespace SourceGenerator.Abstraction.Attributes;

/// <summary>
/// 为标注的方法启用缓存（仅对有返回值的方法生效）。
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class CacheableAttribute : ProxyBehaviorAttribute<SourceGenerator.Runtime.CacheableBehavior>
{
    /// <summary>
    /// 缓存有效期（秒）。
    /// </summary>
    public int TtlSeconds { get; set; } = 60;
}
