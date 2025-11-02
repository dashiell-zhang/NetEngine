namespace SourceGenerator.Abstraction.Attributes;

/// <summary>
/// 标记某个接口方法可缓存。仅对有返回值的方法生效。
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class CacheableAttribute : ProxyBehaviorAttribute
{
    /// <summary>
    /// 构造函数：传入运行时行为类型，例如 typeof(SourceGenerator.Runtime.CachingBehavior)。
    /// </summary>
    public CacheableAttribute(Type behaviorType) : base(behaviorType) { }

    /// <summary>
    /// 缓存有效期（秒）
    /// </summary>
    public int TtlSeconds { get; set; } = 60;
}

