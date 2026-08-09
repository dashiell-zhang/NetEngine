using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

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
    /// Redis 锁日志记录器
    /// </summary>
    private readonly ILogger logger;


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
    /// 创建使用空日志记录器的 Redis 分布式锁句柄
    /// </summary>
    /// <param name="database">Redis 数据库</param>
    /// <param name="lockKey">锁键</param>
    /// <param name="lockValue">当前持有者的唯一所有权令牌</param>
    public RedisLockHandle(IDatabase database, string lockKey, string lockValue) : this(database, lockKey, lockValue, NullLogger.Instance)
    {

    }


    /// <summary>
    /// 创建 Redis 分布式锁句柄
    /// </summary>
    /// <param name="database">Redis 数据库</param>
    /// <param name="lockKey">锁键</param>
    /// <param name="lockValue">当前持有者的唯一所有权令牌</param>
    /// <param name="logger">Redis 锁日志记录器</param>
    public RedisLockHandle(IDatabase database, string lockKey, string lockValue, ILogger logger)
    {

        Database = database;
        LockKey = lockKey;
        LockValue = lockValue;
        this.logger = logger;

    }


    /// <summary>
    /// 幂等释放当前持有的 Redis 锁
    /// </summary>
    public void Dispose()
    {

        _ = GetOrStartReleaseTask();

    }


    /// <summary>
    /// 异步释放当前持有的 Redis 锁并等待释放错误完成记录
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
            return releaseTask ??= ReleaseAsync();
        }

    }


    /// <summary>
    /// 释放 Redis 锁并记录错误且不影响业务执行结果
    /// </summary>
    private async Task ReleaseAsync()
    {

        try
        {
            await Database.LockReleaseAsync(LockKey, LockValue);
        }
        catch (Exception ex)
        {
            try
            {
                logger.LogError(ex, "Release Redis distributed lock failed for key {LockKey} Business execution result will be preserved", LockKey);
            }
            catch
            {
            }
        }

    }

}
