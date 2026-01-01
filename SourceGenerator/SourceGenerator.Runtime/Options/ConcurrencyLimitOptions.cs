namespace SourceGenerator.Runtime.Options;

/// <summary>
/// 用于控制代理行为中并发限制能力的配置项
/// </summary>
public class ConcurrencyLimitOptions
{

    /// <summary>
    /// 是否使用入参参与 key 计算
    /// </summary>
    public bool IsUseParameter { get; init; }


    /// <summary>
    /// 未获取到锁时是否直接阻断（否则会等待排队）
    /// </summary>
    public bool IsBlock { get; init; }


    /// <summary>
    /// 锁的失效时长（秒）
    /// </summary>
    public int ExpirySeconds { get; init; }


    /// <summary>
    /// 允许的并发数（信号量）
    /// </summary>
    public int Semaphore { get; init; } = 1;

}
