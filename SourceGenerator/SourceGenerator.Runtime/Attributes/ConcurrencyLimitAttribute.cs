using SourceGenerator.Runtime.Options;
using SourceGenerator.Runtime.Pipeline.Behaviors;

namespace SourceGenerator.Runtime.Attributes;

/// <summary>
/// 为标注的方法开启基于分布式锁的并发数限制（按方法入参作为 key）
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class ConcurrencyLimitAttribute : ProxyBehaviorAttribute<ConcurrencyLimitBehavior, ConcurrencyLimitOptions>
{

    /// <summary>
    /// 是否使用入参参与 key 计算
    /// </summary>
    public bool IsUseParameter { get; set; }


    /// <summary>
    /// 未获取到锁时是否直接阻断（否则会等待排队）
    /// </summary>
    public bool IsBlock { get; set; }


    /// <summary>
    /// 锁的失效时长（秒）
    /// </summary>
    public int ExpirySeconds { get; set; } = 0;


    /// <summary>
    /// 允许的并发数（信号量）
    /// </summary>
    public int Semaphore { get; set; } = 1;

}
