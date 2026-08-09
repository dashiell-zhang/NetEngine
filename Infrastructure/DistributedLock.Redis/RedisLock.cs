using DistributedLock.Redis.Models;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
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
    /// 创建 Redis 分布式锁实例
    /// </summary>
    /// <param name="config">Redis 配置监视器</param>
    public RedisLock(IOptionsMonitor<RedisSetting> config)
    {

        redisSetting = config.CurrentValue;

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
    /// <returns>成功获取的锁句柄</returns>
    public async Task<IDistributedLockHandle> LockAsync(string key, TimeSpan expiry = default, int semaphore = 1)
    {

        if (expiry == default)
        {
            expiry = TimeSpan.FromMinutes(1);
        }

        var endTime = DateTime.UtcNow + expiry;

        var keyMd5 = redisSetting.InstanceName + Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(key)));

    StartTag:
        {
            for (int i = 0; i < semaphore; i++)
            {
                var tempKey = keyMd5 + " " + i;

                try
                {
                    var database = (await connectionMultiplexer.Value).GetDatabase();
                    var lockValue = CreateLockValue();

                    if (await database.LockTakeAsync(tempKey, lockValue, expiry))
                    {
                        return new RedisLockHandle(database, tempKey, lockValue);
                    }
                }
                catch
                {

                }
            }


            if (DateTime.UtcNow < endTime)
            {
                await Task.Delay(100);
                goto StartTag;
            }

            throw new Exception("获取锁" + key + "超时失败");
        }

    }


    /// <summary>
    /// 尝试立即获取指定名称的 Redis 锁
    /// </summary>
    /// <param name="key">锁名称</param>
    /// <param name="expiry">失效时长</param>
    /// <param name="semaphore">允许同时持有锁的名额数量</param>
    /// <returns>成功返回锁句柄，失败返回 null</returns>
    public async Task<IDistributedLockHandle?> TryLockAsync(string key, TimeSpan expiry = default, int semaphore = 1)
    {

        if (expiry == default)
        {
            expiry = TimeSpan.FromMinutes(1);
        }

        var keyMd5 = redisSetting.InstanceName + Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(key)));

        for (int i = 0; i < semaphore; i++)
        {
            var tempKey = keyMd5 + " " + i;

            try
            {
                var database = (await connectionMultiplexer.Value).GetDatabase();
                var lockValue = CreateLockValue();

                if (await database.LockTakeAsync(tempKey, lockValue, expiry))
                {
                    return new RedisLockHandle(database, tempKey, lockValue);
                }
            }
            catch
            {

            }
        }

        return null;

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
