namespace EventBus;

/// <summary>
/// 标记由当前进程处理的广播事件监听方法
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public sealed class EventSubscribeAttribute(string name) : Attribute
{

    /// <summary>
    /// 监听的事件名称，区分大小写
    /// </summary>
    public string Name { get; } = EventSerialization.ValidateName(name);

}
