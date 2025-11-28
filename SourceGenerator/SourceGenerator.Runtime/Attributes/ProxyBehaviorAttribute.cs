namespace SourceGenerator.Runtime.Attributes;


/// <summary>
/// 统一的代理行为基类 Attribute。用于在编译期把方法上的行为按声明顺序拼装进运行时管道。
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public abstract class ProxyBehaviorAttribute : Attribute
{
    /// <summary>
    /// 运行时行为类型（必须实现 IInvocationBehavior）。生成器主要通过泛型基类识别行为与可选项类型，BehaviorType 作为兜底。
    /// </summary>
    public Type BehaviorType { get; }

    protected ProxyBehaviorAttribute(Type behaviorType)
    {
        BehaviorType = behaviorType ?? throw new ArgumentNullException(nameof(behaviorType));
    }
}


/// <summary>
/// 无选项版本，便于在 Attribute 上省略 typeof(...) 的显式书写。
/// 例如：sealed class FooAttribute : ProxyBehaviorAttribute&lt;FooBehavior&gt; { }
/// </summary>
public abstract class ProxyBehaviorAttribute<TBehavior> : ProxyBehaviorAttribute
{
    protected ProxyBehaviorAttribute() : base(typeof(TBehavior)) { }
}


/// <summary>
/// 带选项版本。TOptions 会被生成器映射 Attribute 的同名命名参数到对应可写属性，
/// 并在运行时通过 InvocationContext.Features 传递给行为使用。
/// </summary>
public abstract class ProxyBehaviorAttribute<TBehavior, TOptions> : ProxyBehaviorAttribute
    where TOptions : class
{
    protected ProxyBehaviorAttribute() : base(typeof(TBehavior)) { }
}

