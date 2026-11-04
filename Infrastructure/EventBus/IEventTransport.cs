namespace EventBus;

/// <summary>
/// 保存与读取广播事件的可替换存储能力
/// </summary>
public interface IEventTransport
{

    /// <summary>
    /// 建立仅包含当前已提交事件的启动位置
    /// </summary>
    Task<string> GetPositionAsync(CancellationToken cancellationToken = default);


    /// <summary>
    /// 独立保存事件并等待后端确认
    /// </summary>
    Task AppendAsync(EventEnvelope message, CancellationToken cancellationToken = default);


    /// <summary>
    /// 按后端提交顺序读取指定位置之后的事件
    /// </summary>
    Task<IReadOnlyList<EventRecord>> ReadAsync(string position, int count, CancellationToken cancellationToken = default);


    /// <summary>
    /// 物理清理指定时间之前的事件并保留流的读取位置
    /// </summary>
    Task TrimAsync(DateTimeOffset before, CancellationToken cancellationToken = default);

}

/// <summary>
/// 在不同存储后端之间共用的事件内容
/// </summary>
/// <param name="EventId">事件标识</param>
/// <param name="EventName">共享事件类型的完整名称</param>
/// <param name="Payload">事件参数的 JSON 内容</param>
public sealed record EventEnvelope(Guid EventId, string EventName, string Payload);

/// <summary>
/// 已保存事件及其后端读取位置
/// </summary>
/// <param name="Position">由后端解释的读取位置</param>
/// <param name="PublishedAt">后端记录事件的时间</param>
/// <param name="Message">事件内容</param>
public sealed record EventRecord(string Position, DateTimeOffset PublishedAt, EventEnvelope Message);
