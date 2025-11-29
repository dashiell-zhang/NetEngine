namespace SourceGenerator.Runtime.Attributes;
/// <summary>
/// 标记目标类需要由源生成器生成派生代理类
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class AutoProxyAttribute : Attribute
{
}

