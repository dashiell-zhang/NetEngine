using SourceGenerator.Runtime.Pipeline.Behaviors;

namespace SourceGenerator.Runtime.Attributes;

/// <summary>
/// 为标注的方法开启调用日志记录
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class LoggingAttribute : ProxyBehaviorAttribute<LoggingBehavior>
{
}

