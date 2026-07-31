using StackExchange.Redis;
using System.Diagnostics;

namespace DistributedLock.Redis;

/// <summary>
/// Redis 分布式锁句柄
/// </summary>
public sealed class RedisLockHandle : IDisposable
{

    /// <summary>
    /// 是否已经触发释放
    /// </summary>
    private int disposed;


    /// <summary>
    /// Redis 数据库
    /// </summary>
    public IDatabase Database { get; }


    /// <summary>
    /// 锁键
    /// </summary>
    public string LockKey { get; }


    /// <summary>
    /// 当前持有者的唯一所有权令牌
    /// </summary>
    public string LockValue { get; }


    /// <summary>
    /// 当前句柄是否已经释放
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref disposed) != 0;


    /// <summary>
    /// 创建 Redis 分布式锁句柄
    /// </summary>
    /// <param name="database">Redis 数据库</param>
    /// <param name="lockKey">锁键</param>
    /// <param name="lockValue">当前持有者的唯一所有权令牌</param>
    public RedisLockHandle(IDatabase database, string lockKey, string lockValue)
    {

        Database = database;
        LockKey = lockKey;
        LockValue = lockValue;

    }


    /// <summary>
    /// 幂等释放当前持有的 Redis 锁
    /// </summary>
    public void Dispose()
    {

        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        _ = ReleaseAsync();

    }


    /// <summary>
    /// 异步释放当前持有的 Redis 锁并观察释放异常
    /// </summary>
    private async Task ReleaseAsync()
    {

        try
        {
            await Database.LockReleaseAsync(LockKey, LockValue);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("释放 Redis 锁失败 {0}: {1}", LockKey, ex.Message);
        }

    }

}
