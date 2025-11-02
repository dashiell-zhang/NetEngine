namespace SourceGenerator.Abstraction.Attributes;

/// <summary>
/// 统一的代理行为基类 Attribute。用于在编译期把方法上的行为按声明顺序拼装进运行时管道。
/// 行为类型通过构造函数传入（例如 typeof(SourceGenerator.Runtime.RetryBehavior)）。
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public abstract class ProxyBehaviorAttribute : Attribute
{
    /// <summary>
    /// 运行时行为类型（必须是实现 IInvocationBehavior 的类型）。
    /// </summary>
    public Type BehaviorType { get; }

    protected ProxyBehaviorAttribute(Type behaviorType)
    {
        BehaviorType = behaviorType ?? throw new ArgumentNullException(nameof(behaviorType));
    }
}

