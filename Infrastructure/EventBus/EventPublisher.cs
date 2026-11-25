using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace EventBus;

/// <summary>
/// 按名称发布可携带 JSON 参数的事件
/// </summary>
internal sealed class EventPublisher(IServiceProvider services) : IEventPublisher
{

    /// <summary>
    /// 校验事件名称并将序列化的参数保存到后端
    /// </summary>
    public Task PublishAsync(string name, object? parameter, CancellationToken cancellationToken = default)
    {

        EventSerialization.ValidateName(name);
        cancellationToken.ThrowIfCancellationRequested();
        var payload = parameter is null ? null : JsonSerializer.Serialize(parameter, EventSerialization.Options);
        var transport = services.GetRequiredService<IEventTransport>();
        return transport.AppendAsync(new EventEnvelope(Guid.NewGuid(), name, payload), cancellationToken);

    }

}

/// <summary>
/// 统一事件名称校验与参数的 JSON 序列化约定
/// </summary>
internal static class EventSerialization
{

    /// <summary>
    /// 发布和监听共用的 JSON 配置
    /// </summary>
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);


    /// <summary>
    /// 校验发布方和订阅方使用的事件名称
    /// </summary>
    internal static string ValidateName(string name)
    {

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 1024)
        {
            throw new ArgumentException("事件名称不能超过 1024 个字符", nameof(name));
        }

        return name;

    }

}
