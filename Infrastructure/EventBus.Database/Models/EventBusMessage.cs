namespace EventBus.Database.Models;

/// <summary>
/// 保存可由所有订阅实例独立读取的广播事件
/// </summary>
public sealed class EventBusMessage
{

    /// <summary>
    /// 所属广播范围
    /// </summary>
    public string Scope { get; set; } = "";


    /// <summary>
    /// 范围内遵循提交顺序的读取位置
    /// </summary>
    public long Position { get; set; }


    /// <summary>
    /// 广播事件标识
    /// </summary>
    public Guid EventId { get; set; }


    /// <summary>
    /// 共享事件类型的完整名称
    /// </summary>
    public string EventName { get; set; } = "";


    /// <summary>
    /// 使用统一约定序列化的事件参数
    /// </summary>
    public string Payload { get; set; } = "";


    /// <summary>
    /// 当前事件保存时的 UTC 时间
    /// </summary>
    public DateTimeOffset PublishedAt { get; set; }

}
