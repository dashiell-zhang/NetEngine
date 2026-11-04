namespace EventBus;

/// <summary>
/// 选择宿主使用的事件存储后端
/// </summary>
public enum EventBusProvider
{
    /// <summary>
    /// 使用 Redis Streams
    /// </summary>
    Redis,

    /// <summary>
    /// 使用 EF Core 关系数据库
    /// </summary>
    Database
}

/// <summary>
/// 配置广播范围与读取和重试周期
/// </summary>
public sealed class EventBusOptions
{

    /// <summary>
    /// 当前宿主选择的事件存储后端
    /// </summary>
    public EventBusProvider Provider { get; set; } = EventBusProvider.Redis;


    /// <summary>
    /// 互通实例共用的项目和环境范围
    /// </summary>
    public string Scope { get; set; } = "NetEngine";


    /// <summary>
    /// 断线补收所需的历史保留时间
    /// </summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromHours(1);


    /// <summary>
    /// 没有新事件或后端断线时的读取间隔
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);


    /// <summary>
    /// 监听方法失败后的重试间隔
    /// </summary>
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(1);


    /// <summary>
    /// 清理过期事件的间隔
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(1);


    /// <summary>
    /// 每次读取的事件条数
    /// </summary>
    public int BatchSize { get; set; } = 100;

}
