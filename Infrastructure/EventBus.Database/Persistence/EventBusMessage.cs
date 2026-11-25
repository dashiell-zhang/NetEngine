using Repository.Core.Bases;

namespace EventBus.Database.Persistence;

/// <summary>
/// 保存可由所有订阅实例独立读取的广播事件
/// </summary>
public sealed class EventBusMessage : CD
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
    /// 发布方和监听方约定的事件名称
    /// </summary>
    public string EventName { get; set; } = "";


    /// <summary>
    /// 事件参数的 JSON 文本，无参数时为数据库 NULL
    /// </summary>
    public string? Payload { get; set; }



}
