namespace SourceGenerator.Runtime.Options;

/// <summary>
/// 用于控制代理行为中自动重试能力的配置项
/// </summary>
public class RetryOptions
{

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; init; } = 3;


    /// <summary>
    /// 每次重试前的等待时长（秒），0 表示不等待
    /// </summary>
    public int DelaySeconds { get; init; } = 0;

}
