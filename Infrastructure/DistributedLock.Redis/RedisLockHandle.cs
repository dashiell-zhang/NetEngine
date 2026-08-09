using StackExchange.Redis;
using System.Diagnostics;

namespace DistributedLock.Redis;

/// <summary>
/// Redis 分布式锁句柄
/// </summary>
public sealed class RedisLockHandle : IDistributedLockHandle
{

    /// <summary>
    /// 释放任务同步对象
    /// </summary>
    private readonly object releaseLock = new();


    /// <summary>
    /// 已经启动的唯一释放任务
    /// </summary>
    private Task? releaseTask;


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
    public bool IsDisposed
    {
        get
        {

            lock (releaseLock)
            {
                return releaseTask is not null;
            }

        }
    }


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

        _ = ObserveReleaseAsync(GetOrStartReleaseTask());

    }


    /// <summary>
    /// 异步释放当前持有的 Redis 锁并向调用方传播释放异常
    /// </summary>
    public ValueTask DisposeAsync()
    {

        return new ValueTask(GetOrStartReleaseTask());

    }


    /// <summary>
    /// 获取或启动唯一的 Redis 解锁任务
    /// </summary>
    /// <returns>当前句柄共享的释放任务</returns>
    private Task GetOrStartReleaseTask()
    {

        lock (releaseLock)
        {
            return releaseTask ??= Database.LockReleaseAsync(LockKey, LockValue);
        }

    }


    /// <summary>
    /// 为同步兼容入口观察并记录异步释放异常
    /// </summary>
    /// <param name="task">需要观察的释放任务</param>
    private async Task ObserveReleaseAsync(Task task)
    {

        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("释放 Redis 锁失败 {0}: {1}", LockKey, ex.Message);
        }

    }

}
