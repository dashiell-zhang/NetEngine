namespace SourceGenerator.Runtime.Options;

/// <summary>
/// 用于控制代理行为中缓存能力的配置项
/// </summary>
public sealed class CacheableOptions
{
    /// <summary>
    /// 缓存数据的生存时间 秒
    /// </summary>
    public int TtlSeconds { get; init; }
}
