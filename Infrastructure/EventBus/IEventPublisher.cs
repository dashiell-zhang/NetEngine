namespace EventBus;

/// <summary>
/// 发布跨进程广播事件
/// </summary>
public interface IEventPublisher
{

    /// <summary>
    /// 将事件保存到后端，失败时向调用方抛出异常
    /// </summary>
    Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default) where TEvent : class;

}
