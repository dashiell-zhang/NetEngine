namespace EventBus;

/// <summary>
/// 标记由当前进程处理的广播事件监听方法
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public sealed class EventSubscribeAttribute : Attribute
{

}
