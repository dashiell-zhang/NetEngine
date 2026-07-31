using SourceGenerator.Runtime.Options;
using SourceGenerator.Runtime.Pipeline.Behaviors;

namespace SourceGenerator.Runtime.Attributes;

/// <summary>
/// 为标注的方法开启自动重试 非取消异常出现时最多额外重试指定次数
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class RetryAttribute : ProxyBehaviorAttribute<RetryBehavior, RetryOptions>
{

    /// <summary>
    /// 最大额外重试次数 默认 3 次 不包含首次执行
    /// </summary>
    public int MaxRetries { get; set; } = 3;


    /// <summary>
    /// 每次重试前的等待时长（秒），默认 0 表示不等待
    /// </summary>
    public int DelaySeconds { get; set; } = 0;

}
