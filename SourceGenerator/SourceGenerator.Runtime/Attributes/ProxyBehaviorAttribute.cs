namespace SourceGenerator.Runtime.Attributes;


/// <summary>
/// 统一标记由生成器识别的代理行为特性
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public abstract class ProxyBehaviorAttribute : Attribute
{

}


/// <summary>
/// 通过 TBehavior 声明行为类型的无选项代理行为特性基类
/// 例如：sealed class FooAttribute : ProxyBehaviorAttribute&lt;FooBehavior&gt; { }
/// </summary>
public abstract class ProxyBehaviorAttribute<TBehavior> : ProxyBehaviorAttribute
{

}


/// <summary>
/// 通过 TBehavior 和 TOptions 声明行为及配置类型的代理行为特性基类
/// TOptions 会由生成器映射 Attribute 中显式提供的同名命名参数到对应可写属性
/// 未显式提供的配置使用 TOptions 自身默认值并通过 InvocationContext.Features 传递给行为
/// 行为特性构造函数参数不参与配置映射并会由生成器报告诊断
/// </summary>
public abstract class ProxyBehaviorAttribute<TBehavior, TOptions> : ProxyBehaviorAttribute
    where TOptions : class
{

}
