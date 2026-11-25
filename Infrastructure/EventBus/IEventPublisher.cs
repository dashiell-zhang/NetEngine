namespace EventBus;

/// <summary>
/// 发布跨进程广播事件
/// </summary>
public interface IEventPublisher
{

    /// <summary>
    /// 按名称发布无参数事件并等待后端确认
    /// </summary>
    Task PublishAsync(string name, CancellationToken cancellationToken = default) => PublishAsync(name, null, cancellationToken);


    /// <summary>
    /// 按名称发布事件并等待后端确认，参数按 JSON 传输
    /// </summary>
    Task PublishAsync(string name, object? parameter, CancellationToken cancellationToken = default);

}
