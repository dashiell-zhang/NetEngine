namespace EventBus.Database.Models;

/// <summary>
/// 通过提交顺序维护广播范围的最新位置
/// </summary>
public sealed class EventBusStream
{

    /// <summary>
    /// 互通实例共用的广播范围
    /// </summary>
    public string Scope { get; set; } = "";


    /// <summary>
    /// 当前已经提交的最大事件位置
    /// </summary>
    public long Position { get; set; }


    /// <summary>
    /// 应用管理的跨数据库并发令牌
    /// </summary>
    public Guid Version { get; set; } = Guid.NewGuid();

}
