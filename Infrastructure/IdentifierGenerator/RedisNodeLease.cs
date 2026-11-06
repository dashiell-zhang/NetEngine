using IdentifierGenerator.Models;
using StackExchange.Redis;
using System.Diagnostics;
using System.Security.Cryptography;

namespace IdentifierGenerator;

/// <summary>
/// 原子占用雪花节点并通过所有者令牌续期，不使用业务缓存辅助方法
/// </summary>
internal sealed class RedisNodeLease : IDisposable, IAsyncDisposable
{

    private const string CheckScript = "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('PTTL', KEYS[1]) else return -1 end";
    private const string RenewScript = "if redis.call('GET', KEYS[1]) == ARGV[1] and redis.call('PTTL', KEYS[1]) > 0 then redis.call('PEXPIRE', KEYS[1], ARGV[2]); return ARGV[2] else return -1 end";
    private readonly ConnectionMultiplexer connection;
    private readonly IDatabase database;
    private readonly string key;
    private readonly string owner;
    private readonly TimeSpan duration;
    private readonly TimeSpan safetyMargin;
    private readonly CancellationTokenSource stopping = new();
    private readonly Task renewal;
    private long validUntil;
    private int lostOwner;
    private int disposed;


    /// <summary>
    /// 在原子占用并确认所有权后立即启动独立续期循环
    /// </summary>
    private RedisNodeLease(ConnectionMultiplexer connection, string key, string owner, int node, TimeSpan duration, long confirmedAt, long remainingMilliseconds)
    {

        this.connection = connection;
        database = connection.GetDatabase();
        this.key = key;
        this.owner = owner;
        this.duration = duration;
        safetyMargin = TimeSpan.FromMilliseconds(Math.Min(duration.TotalMilliseconds / 10, 5000));
        Node = node;
        SetValidity(confirmedAt, remainingMilliseconds);
        EnsureValid();
        renewal = RenewLoopAsync();

    }


    /// <summary>
    /// 已确认占用的五位数据中心与五位机器组合
    /// </summary>
    public int Node { get; }


    /// <summary>
    /// 通过 SET NX 与过期时间原子占用候选节点，竞争失败时继续选择
    /// </summary>
    public static async Task<RedisNodeLease> AcquireAsync(IdSetting settings)
    {

        var connection = await ConnectionMultiplexer.ConnectAsync(settings.RedisConfiguration).ConfigureAwait(false);
        try
        {
            var database = connection.GetDatabase();
            var owner = Guid.NewGuid().ToString("N");
            var start = RandomNumberGenerator.GetInt32(1024);
            for (var offset = 0; offset < 1024; offset++)
            {
                var node = (start + offset) % 1024;
                var key = $"{settings.InstanceName}:Node:{node}";
                if (!await database.StringSetAsync(key, owner, settings.LeaseDuration, When.NotExists).ConfigureAwait(false))
                {
                    continue;
                }

                var requestedAt = Stopwatch.GetTimestamp();
                var remaining = (long)await database.ScriptEvaluateAsync(CheckScript, [key], [owner]).ConfigureAwait(false);
                if (remaining <= 0)
                {
                    throw new InvalidOperationException("ID 节点占用成功后未能确认有效所有权");
                }
                return new RedisNodeLease(connection, key, owner, node, settings.LeaseDuration, requestedAt, remaining);
            }

            throw new InvalidOperationException("ID 节点的 1024 个组合均已占用");
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }

    }


    /// <summary>
    /// 只在已确认且仍处于本地保守有效期内时允许发号
    /// </summary>
    public void EnsureValid()
    {

        if (Volatile.Read(ref disposed) != 0 || Volatile.Read(ref lostOwner) != 0 || Stopwatch.GetTimestamp() >= Interlocked.Read(ref validUntil))
        {
            throw new InvalidOperationException("ID 节点租约尚未确认或已经失效，禁止生成 ID");
        }

    }


    /// <summary>
    /// 续期只更新同一所有者的有效键，失败不延长本地有效时间
    /// </summary>
    private async Task RenewLoopAsync()
    {

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(Math.Min(duration.TotalMilliseconds / 3, 30000)));
        try
        {
            while (await timer.WaitForNextTickAsync(stopping.Token).ConfigureAwait(false))
            {
                try
                {
                    var requestedAt = Stopwatch.GetTimestamp();
                    var remaining = (long)await database.ScriptEvaluateAsync(RenewScript, [key], [owner, (long)duration.TotalMilliseconds]).WaitAsync(stopping.Token).ConfigureAwait(false);
                    if (remaining <= 0)
                    {
                        Volatile.Write(ref lostOwner, 1);
                        Console.Error.WriteLine("IdentifierGenerator: 节点所有权丢失，已停止发号，需要重新启动实例");
                        return;
                    }

                    SetValidity(requestedAt, remaining);
                }
                catch (OperationCanceledException) when (stopping.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine($"IdentifierGenerator: 节点续期失败（{exception.GetType().Name}），不延长本地有效期");
                }
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
        }

    }


    /// <summary>
    /// 从请求发起时刻计算保守有效期，扣除续期通信耗时及安全余量
    /// </summary>
    private void SetValidity(long requestedAt, long remainingMilliseconds)
    {

        var validTicks = (long)((remainingMilliseconds - safetyMargin.TotalMilliseconds) / 1000 * Stopwatch.Frequency);
        Interlocked.Exchange(ref validUntil, requestedAt + validTicks);

    }


    /// <summary>
    /// 停止续期且保留 Redis 占用直到自然到期，避免立即重用节点
    /// </summary>
    public void Dispose()
    {

        DisposeAsync().AsTask().GetAwaiter().GetResult();

    }


    /// <summary>
    /// 停止续期并释放本实例连接
    /// </summary>
    public async ValueTask DisposeAsync()
    {

        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        await stopping.CancelAsync().ConfigureAwait(false);
        await renewal.ConfigureAwait(false);
        await connection.DisposeAsync().ConfigureAwait(false);
        stopping.Dispose();

    }

}
