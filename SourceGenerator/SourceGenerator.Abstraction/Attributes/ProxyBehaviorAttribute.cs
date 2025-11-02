namespace SourceGenerator.Abstraction.Attributes;

/// <summary>
/// 统一的代理行为基类 Attribute。用于在编译期把方法上的行为按声明顺序拼装进运行时管道。
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public abstract class ProxyBehaviorAttribute : Attribute
{
    /// <summary>
    /// 运行时行为类型（必须是实现 IInvocationBehavior 的类型）。
    /// 注意：生成器主要通过类型系统（泛型基类）识别行为类型，BehaviorType 作为备选。
    /// </summary>
    public Type BehaviorType { get; }

    protected ProxyBehaviorAttribute(Type behaviorType)
    {
        BehaviorType = behaviorType ?? throw new ArgumentNullException(nameof(behaviorType));
    }
}

/// <summary>
/// 泛型版本，便于在 Attribute 上省略 typeof(...) 的显式书写：
/// 例如：sealed class CacheableAttribute : ProxyBehaviorAttribute<CachingBehavior> { }
/// </summary>
public abstract class ProxyBehaviorAttribute<TBehavior> : ProxyBehaviorAttribute
{
    protected ProxyBehaviorAttribute() : base(typeof(TBehavior)) { }
}
