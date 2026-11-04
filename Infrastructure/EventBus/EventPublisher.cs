using System.Text.Json;

namespace EventBus;

/// <summary>
/// 使用统一序列化约定发布强类型事件
/// </summary>
internal sealed class EventPublisher(IEventTransport transport) : IEventPublisher
{

    /// <summary>
    /// 序列化明确的事件契约并保存到后端
    /// </summary>
    public Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default) where TEvent : class
    {

        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        var name = EventSerialization.GetName(typeof(TEvent));
        if (message.GetType() != typeof(TEvent))
        {
            throw new ArgumentException("请使用事件的具体类型发布，不支持隐式多态事件", nameof(message));
        }

        return transport.AppendAsync(new EventEnvelope(Guid.NewGuid(), name, JsonSerializer.Serialize(message, EventSerialization.Options)), cancellationToken);

    }

}

/// <summary>
/// 统一事件类型名称与 JSON 序列化约定
/// </summary>
internal static class EventSerialization
{

    /// <summary>
    /// 发布和监听共用的 JSON 配置
    /// </summary>
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);


    /// <summary>
    /// 校验具体事件类型并取得稳定名称
    /// </summary>
    internal static string GetName(Type type)
    {

        if (!type.IsClass || type.IsAbstract || type.IsGenericType || type == typeof(object) || type.FullName == null)
        {
            throw new ArgumentException($"事件必须是具有完整名称的非泛型具体类：{type}");
        }

        return type.FullName;

    }

}
