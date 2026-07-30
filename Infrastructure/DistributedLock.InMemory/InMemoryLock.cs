using DistributedLock.InMemory.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DistributedLock.InMemory;

/// <summary>
/// 基于进程内内存的锁实现
/// 不支持跨进程与跨机器互斥
/// </summary>
public sealed class InMemoryLock : IDistributedLock
{

    /// <summary>
    /// 等待锁时的最大轮询间隔
    /// </summary>
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(100);


    /// <summary>
    /// 以 key 为粒度的锁分组表
    /// </summary>
    private static readonly ConcurrentDictionary<string, LockGroup> Groups = new(StringComparer.Ordinal);


    /// <summary>
    /// 续期指定锁句柄
    /// </summary>
    /// <param name="lockHandle">锁句柄</param>
    /// <param name="expiry">新的失效时长</param>
    /// <returns>续期是否成功</returns>
    public Task<bool> RenewAsync(IDisposable lockHandle, TimeSpan expiry)
    {

        if (expiry == default)
        {
            expiry = TimeSpan.FromMinutes(1);
        }

        if (expiry <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(expiry), "expiry 必须大于 0");
        }

        return Task.FromResult(lockHandle is InMemoryLockHandle inMemoryLockHandle && inMemoryLockHandle.Renew(expiry));

    }


    /// <summary>
    /// 等待并获取指定名称的锁
    /// </summary>
    /// <param name="key">锁名称</param>
    /// <param name="expiry">锁的失效时长与最长等待时长</param>
    /// <param name="semaphore">可同时持有锁的名额数量</param>
    /// <returns>成功获取的锁句柄</returns>
    public async Task<IDisposable> LockAsync(string key, TimeSpan expiry = default, int semaphore = 1)
    {

        var handle = await TryAcquireAsync(key, expiry, semaphore, wait: true);
        return handle ?? throw new TimeoutException("获取锁" + key + "超时失败");

    }


    /// <summary>
    /// 尝试立即获取指定名称的锁
    /// </summary>
    /// <param name="key">锁名称</param>
    /// <param name="expiry">锁的失效时长</param>
    /// <param name="semaphore">可同时持有锁的名额数量</param>
    /// <returns>成功返回锁句柄 失败返回 null</returns>
    public Task<IDisposable?> TryLockAsync(string key, TimeSpan expiry = default, int semaphore = 1)
    {

        return TryAcquireAsync(key, expiry, semaphore, wait: false);

    }


    /// <summary>
    /// 执行一次锁获取逻辑
    /// </summary>
    /// <param name="key">锁的名称</param>
    /// <param name="expiry">锁的失效时长</param>
    /// <param name="semaphore">可同时持有锁的名额数量</param>
    /// <param name="wait">是否等待直到超时</param>
    /// <returns>成功返回句柄 失败返回 null</returns>
    private static async Task<IDisposable?> TryAcquireAsync(string key, TimeSpan expiry, int semaphore, bool wait)
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

        var group = AcquireGroup(key);
        var groupReferenceTransferred = false;
        var startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            while (true)
            {
                for (int i = 0; i < semaphore; i++)
                {
                    var slot = group.Slots.GetOrAdd(i, static _ => new SemaphoreSlim(1, 1));

                    if (await slot.WaitAsync(0))
                    {
                        try
                        {
                            var handle = new InMemoryLockHandle(key, group, slot, expiry);
                            groupReferenceTransferred = true;
                            return handle;
                        }
                        catch
                        {
                            slot.Release();
                            throw;
                        }
                    }
                }

                if (!wait)
                {
                    return null;
                }

                var remaining = expiry - Stopwatch.GetElapsedTime(startTimestamp);
                if (remaining <= TimeSpan.Zero)
                {
                    return null;
                }

                await Task.Delay(remaining < RetryInterval ? remaining : RetryInterval);

                if (Stopwatch.GetElapsedTime(startTimestamp) >= expiry)
                {
                    return null;
                }
            }
        }
        finally
        {
            if (!groupReferenceTransferred)
            {
                ReleaseGroup(key, group);
            }
        }

    }


    /// <summary>
    /// 获取并保留指定名称的锁分组
    /// </summary>
    /// <param name="key">锁名称</param>
    /// <returns>已保留引用的锁分组</returns>
    private static LockGroup AcquireGroup(string key)
    {

        while (true)
        {
            var group = Groups.GetOrAdd(key, static _ => new LockGroup());

            lock (group.SyncRoot)
            {
                if (group.Removed)
                {
                    continue;
                }

                group.ReferenceCount++;
                return group;
            }
        }

    }


    /// <summary>
    /// 释放锁分组引用并在无人使用时移除分组
    /// </summary>
    /// <param name="key">锁名称</param>
    /// <param name="group">锁分组</param>
    internal static void ReleaseGroup(string key, LockGroup group)
    {

        lock (group.SyncRoot)
        {
            group.ReferenceCount--;

            if (group.ReferenceCount < 0)
            {
                throw new InvalidOperationException("锁分组引用计数不能小于 0");
            }

            if (group.ReferenceCount != 0)
            {
                return;
            }

            group.Removed = true;
            Groups.TryRemove(new KeyValuePair<string, LockGroup>(key, group));
        }

    }

}
