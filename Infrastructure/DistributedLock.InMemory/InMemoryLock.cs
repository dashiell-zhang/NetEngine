using DistributedLock.InMemory.Models;
using System.Collections.Concurrent;

namespace DistributedLock.InMemory;

/// <summary>
/// 基于进程内内存的锁实现
/// 不支持跨进程与跨机器互斥
/// </summary>
public sealed class InMemoryLock : IDistributedLock
{

    /// <summary>
    /// 以 key 为粒度的锁分组表
    /// </summary>
    private static readonly ConcurrentDictionary<string, LockGroup> Groups = new(StringComparer.Ordinal);


    public async Task<IDisposable> LockAsync(string key, TimeSpan expiry = default, int semaphore = 1)
    {
        var handle = await TryAcquireAsync(key, expiry, semaphore, wait: true);
        return handle ?? throw new Exception("获取锁" + key + "超时失败");
    }


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

        var group = Groups.GetOrAdd(key, static _ => new LockGroup());
        var endTime = DateTime.UtcNow + expiry;

    StartTag:
        {
            for (int i = 0; i < semaphore; i++)
            {
                var slot = group.Slots.GetOrAdd(i, static _ => new SemaphoreSlim(1, 1));

                try
                {
                    if (await slot.WaitAsync(0))
                    {
                        Interlocked.Increment(ref group.ActiveHandles);
                        return new InMemoryLockHandle(key, group, slot, expiry);
                    }
                }
                catch
                {
                }
            }

            if (!wait)
            {
                return null;
            }

            if (DateTime.UtcNow < endTime)
            {
                await Task.Delay(100);
                goto StartTag;
            }
        }

        return null;
    }


    /// <summary>
    /// 当锁分组已无活跃持有者时尝试移除分组
    /// </summary>
    /// <param name="key">锁名称</param>
    /// <param name="group">锁分组</param>
    internal static void TryRemoveGroup(string key, LockGroup group)
    {
        if (Groups.TryGetValue(key, out var current) && ReferenceEquals(current, group))
        {
            Groups.TryRemove(new KeyValuePair<string, LockGroup>(key, group));
        }
    }

}
