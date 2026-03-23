using SourceGenerator.Runtime.Options;
using SourceGenerator.Runtime.Pipeline.Behaviors;

namespace SourceGenerator.Runtime.Attributes;

/// <summary>
/// 为标注的方法开启自动重试，执行出现异常时最多重试指定次数
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class RetryAttribute : ProxyBehaviorAttribute<RetryBehavior, RetryOptions>
{

    /// <summary>
    /// 最大重试次数，默认 3 次
    /// </summary>
    public int MaxRetries { get; set; } = 3;


    /// <summary>
    /// 每次重试前的等待时长（秒），默认 0 表示不等待
    /// </summary>
    public int DelaySeconds { get; set; } = 0;

}
