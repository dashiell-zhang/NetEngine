using DistributedLock.Redis.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace DistributedLock.Redis;

/// <summary>
/// 基于 Redis 的分布式锁实现
/// </summary>
public class RedisLock : IDistributedLock
{

    /// <summary>
    /// Redis 连接复用实例
    /// </summary>
    private readonly Lazy<Task<ConnectionMultiplexer>> connectionMultiplexer;


    /// <summary>
    /// Redis 锁配置
    /// </summary>
    private readonly RedisSetting redisSetting;


    /// <summary>
    /// Redis 锁日志记录器
    /// </summary>
    private readonly ILogger<RedisLock> logger;


    /// <summary>
    /// 创建 Redis 分布式锁实例
    /// </summary>
    /// <param name="config">Redis 配置监视器</param>
    /// <param name="logger">Redis 锁日志记录器</param>
    public RedisLock(IOptionsMonitor<RedisSetting> config, ILogger<RedisLock>? logger = null)
    {

        redisSetting = config.CurrentValue;
        this.logger = logger ?? NullLogger<RedisLock>.Instance;

        connectionMultiplexer = new(async () => await ConnectionMultiplexer.ConnectAsync(redisSetting.Configuration));

    }


    /// <summary>
    /// 续期当前句柄仍然持有的 Redis 锁
    /// </summary>
    /// <param name="lockHandle">锁句柄</param>
    /// <param name="expiry">新的失效时长</param>
    /// <param name="cancellationToken">取消续期等待的令牌</param>
    /// <returns>是否续期成功</returns>
    public async Task<bool> RenewAsync(IDistributedLockHandle lockHandle, TimeSpan expiry, CancellationToken cancellationToken = default)
    {

        cancellationToken.ThrowIfCancellationRequested();

        if (lockHandle is not RedisLockHandle redisLockHandle || redisLockHandle.IsDisposed)
        {
            return false;
        }

        if (expiry == default)
        {
            expiry = TimeSpan.FromMinutes(1);
        }

        try
        {
            var renewalTask = redisLockHandle.Database.LockExtendAsync(redisLockHandle.LockKey, redisLockHandle.LockValue, expiry);

            try
            {
                return await renewalTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _ = ObserveCompletionAsync(renewalTask);
                throw;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }

    }


    /// <summary>
    /// 观察无法由 StackExchange.Redis 取消的底层续期任务
    /// </summary>
    /// <param name="task">已经提交到底层连接的续期任务</param>
    private static async Task ObserveCompletionAsync(Task task)
    {

        try
        {
            await task;
        }
        catch
        {
        }

    }


    /// <summary>
    /// 等待并获取指定名称的 Redis 锁
    /// </summary>
    /// <param name="key">锁名称</param>
    /// <param name="expiry">失效时长和最长等待时长</param>
    /// <param name="semaphore">允许同时持有锁的名额数量</param>
    /// <param name="cancellationToken">取消锁等待的令牌</param>
    /// <returns>成功获取的锁句柄</returns>
    public async Task<IDistributedLockHandle> LockAsync(string key, TimeSpan expiry = default, int semaphore = 1, CancellationToken cancellationToken = default)
    {

        expiry = ValidateAcquireArguments(key, expiry, semaphore);
        cancellationToken.ThrowIfCancellationRequested();

        var startTimestamp = Stopwatch.GetTimestamp();

        var keyMd5 = redisSetting.InstanceName + Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(key)));
        var database = (await WaitForConnectionAsync(cancellationToken)).GetDatabase();

        while (true)
        {
            for (int i = 0; i < semaphore; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var tempKey = keyMd5 + " " + i;
                var lockValue = CreateLockValue();

                if (await TryTakeAsync(database, tempKey, lockValue, expiry, cancellationToken))
                {
                    return new RedisLockHandle(database, tempKey, lockValue, logger);
                }
            }

            var remaining = expiry - Stopwatch.GetElapsedTime(startTimestamp);
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException("获取锁" + key + "超时失败");
            }

            var retryDelay = remaining < TimeSpan.FromMilliseconds(100) ? remaining : TimeSpan.FromMilliseconds(100);
            await Task.Delay(retryDelay, cancellationToken);
        }

    }


    /// <summary>
    /// 尝试立即获取指定名称的 Redis 锁
    /// </summary>
    /// <param name="key">锁名称</param>
    /// <param name="expiry">失效时长</param>
    /// <param name="semaphore">允许同时持有锁的名额数量</param>
    /// <param name="cancellationToken">取消锁获取的令牌</param>
    /// <returns>成功返回锁句柄，失败返回 null</returns>
    public async Task<IDistributedLockHandle?> TryLockAsync(string key, TimeSpan expiry = default, int semaphore = 1, CancellationToken cancellationToken = default)
    {

        expiry = ValidateAcquireArguments(key, expiry, semaphore);
        cancellationToken.ThrowIfCancellationRequested();

        var keyMd5 = redisSetting.InstanceName + Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(key)));
        var database = (await WaitForConnectionAsync(cancellationToken)).GetDatabase();

        for (int i = 0; i < semaphore; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tempKey = keyMd5 + " " + i;
            var lockValue = CreateLockValue();

            if (await TryTakeAsync(database, tempKey, lockValue, expiry, cancellationToken))
            {
                return new RedisLockHandle(database, tempKey, lockValue, logger);
            }
        }

        return null;

    }


    /// <summary>
    /// 等待 Redis 连接并响应调用方取消
    /// </summary>
    /// <param name="cancellationToken">取消连接等待的令牌</param>
    /// <returns>可复用的 Redis 连接</returns>
    private async Task<ConnectionMultiplexer> WaitForConnectionAsync(CancellationToken cancellationToken)
    {

        var connectionTask = connectionMultiplexer.Value;

        try
        {
            return await connectionTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _ = ObserveCompletionAsync(connectionTask);
            throw;
        }

    }


    /// <summary>
    /// 尝试获取 Redis 锁并在取消竞争时清理可能已经获取的锁
    /// </summary>
    /// <param name="database">Redis 数据库</param>
    /// <param name="lockKey">锁键</param>
    /// <param name="lockValue">锁所有权值</param>
    /// <param name="expiry">锁租约时长</param>
    /// <param name="cancellationToken">取消锁获取的令牌</param>
    /// <returns>是否成功获取锁</returns>
    private async Task<bool> TryTakeAsync(IDatabase database, string lockKey, string lockValue, TimeSpan expiry, CancellationToken cancellationToken)
    {

        var takeTask = database.LockTakeAsync(lockKey, lockValue, expiry);

        try
        {
            return await takeTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _ = ReleaseCanceledTakeAsync(takeTask, database, lockKey, lockValue);
            throw;
        }

    }


    /// <summary>
    /// 清理调用方取消后底层命令可能已经获取的 Redis 锁
    /// </summary>
    /// <param name="takeTask">无法由 StackExchange.Redis 取消的获取任务</param>
    /// <param name="database">Redis 数据库</param>
    /// <param name="lockKey">锁键</param>
    /// <param name="lockValue">锁所有权值</param>
    private async Task ReleaseCanceledTakeAsync(Task<bool> takeTask, IDatabase database, string lockKey, string lockValue)
    {

        try
        {
            if (await takeTask)
            {
                await database.LockReleaseAsync(lockKey, lockValue);
            }
        }
        catch (Exception ex)
        {
            LogErrorSafely(ex, "Cleanup canceled Redis distributed lock acquisition failed for key {LockKey}", lockKey);
        }

    }


    /// <summary>
    /// 记录 Redis 锁错误且防止日志提供程序异常逃逸到锁生命周期
    /// </summary>
    /// <param name="exception">需要记录的异常</param>
    /// <param name="message">结构化日志消息模板</param>
    /// <param name="args">结构化日志参数</param>
    private void LogErrorSafely(Exception exception, string message, params object?[] args)
    {

        try
        {
            logger.LogError(exception, message, args);
        }
        catch
        {
        }

    }


    /// <summary>
    /// 验证并规范化锁获取参数
    /// </summary>
    /// <param name="key">锁名称</param>
    /// <param name="expiry">锁失效时长</param>
    /// <param name="semaphore">允许同时持有锁的名额数量</param>
    /// <returns>规范化后的锁失效时长</returns>
    private static TimeSpan ValidateAcquireArguments(string key, TimeSpan expiry, int semaphore)
    {

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("key 不能为空", nameof(key));
        }

        if (semaphore <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(semaphore), "semaphore 必须大于 0");
        }

        if (expiry == default)
        {
            expiry = TimeSpan.FromMinutes(1);
        }

        if (expiry <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(expiry), "expiry 必须大于 0");
        }

        return expiry;

    }


    /// <summary>
    /// 创建当前锁持有者的唯一所有权令牌
    /// </summary>
    /// <returns>唯一所有权令牌</returns>
    private static string CreateLockValue()
    {

        return Guid.CreateVersion7().ToString("N");

    }

}
