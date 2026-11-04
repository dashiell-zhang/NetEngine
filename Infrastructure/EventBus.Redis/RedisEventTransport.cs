using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Globalization;

namespace EventBus.Redis;

/// <summary>
/// 以 Redis Streams 保存并广播事件
/// </summary>
public sealed class RedisEventTransport(IConnectionMultiplexer connection, IOptions<EventBusOptions> options, ILogger<RedisEventTransport> logger) : IEventTransport
{

    /// <summary>
    /// 当前广播范围使用的事件流键
    /// </summary>
    private readonly RedisKey streamKey = $"eventbus:{options.Value.Scope}";


    /// <summary>
    /// 原子读取当前末尾，空流从零位置开始
    /// </summary>
    public async Task<string> GetPositionAsync(CancellationToken cancellationToken = default)
    {

        cancellationToken.ThrowIfCancellationRequested();
        var entries = await connection.GetDatabase().StreamRangeAsync(streamKey, count: 1, messageOrder: Order.Descending).WaitAsync(cancellationToken);
        return entries.Length == 0 ? "0-0" : entries[0].Id.ToString();

    }


    /// <summary>
    /// 等待 Redis 确认事件写入，不使用竞争消费组
    /// </summary>
    public async Task AppendAsync(EventEnvelope message, CancellationToken cancellationToken = default)
    {

        cancellationToken.ThrowIfCancellationRequested();
        await connection.GetDatabase().StreamAddAsync(streamKey, [
            new NameValueEntry("event-id", message.EventId.ToString("N")),
            new NameValueEntry("event-name", message.EventName),
            new NameValueEntry("payload", message.Payload)
        ]).WaitAsync(cancellationToken);

    }


    /// <summary>
    /// 从当前进程的原位置继续读取事件，保留无效记录的位置以便推进游标
    /// </summary>
    public async Task<IReadOnlyList<EventRecord>> ReadAsync(string position, int count, CancellationToken cancellationToken = default)
    {

        cancellationToken.ThrowIfCancellationRequested();
        var entries = await connection.GetDatabase().StreamReadAsync(streamKey, position, count).WaitAsync(cancellationToken);
        return entries.Select(ReadRecord).ToArray();

    }


    /// <summary>
    /// 逐条解析事件，格式错误时记录告警并返回仅用于推进位置的空消息
    /// </summary>
    private EventRecord ReadRecord(StreamEntry entry)
    {

        var id = entry.Id.ToString();
        try
        {
            var fields = entry.Values.ToDictionary(t => t.Name.ToString(), t => t.Value.ToString());
            var publishedAt = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(id.AsSpan(0, id.IndexOf('-')), CultureInfo.InvariantCulture));
            if (string.IsNullOrWhiteSpace(fields["event-name"]))
            {
                throw new FormatException("广播事件名称不能为空");
            }

            return new EventRecord(id, publishedAt, new EventEnvelope(Guid.Parse(fields["event-id"]), fields["event-name"], fields["payload"]));
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException or KeyNotFoundException)
        {
            logger.LogWarning(ex, "跳过格式无效的广播记录 StreamKey={StreamKey} Position={Position}", streamKey.ToString(), id);
            return new EventRecord(id, default, null);
        }

    }


    /// <summary>
    /// 按发布时间裁剪历史，保留流本身及其位置
    /// </summary>
    public async Task TrimAsync(DateTimeOffset before, CancellationToken cancellationToken = default)
    {

        cancellationToken.ThrowIfCancellationRequested();
        var minId = $"{Math.Max(0, before.ToUnixTimeMilliseconds()).ToString(CultureInfo.InvariantCulture)}-0";
        await connection.GetDatabase().StreamTrimByMinIdAsync(streamKey, minId, false).WaitAsync(cancellationToken);

    }

}
