namespace DistributedLock;

/// <summary>
/// 定义分布式锁的获取和租约续期能力
/// </summary>
public interface IDistributedLock
{

    /// <summary>
    /// 获取锁
    /// </summary>
    /// <param name="key">锁的名称，不可重复</param>
    /// <param name="expiry">失效时长</param>
    /// <param name="semaphore">信号量</param>
    /// <returns>成功获取的锁句柄</returns>
    Task<IDistributedLockHandle> LockAsync(string key, TimeSpan expiry = default, int semaphore = 1);


    /// <summary>
    /// 尝试获取锁
    /// </summary>
    /// <param name="key">锁的名称，不可重复</param>
    /// <param name="expiry">失效时长</param>
    /// <param name="semaphore">信号量</param>
    /// <returns>成功获取的锁句柄 获取失败时返回 null</returns>
    Task<IDistributedLockHandle?> TryLockAsync(string key, TimeSpan expiry = default, int semaphore = 1);


    /// <summary>
    /// 续期锁
    /// </summary>
    /// <param name="lockHandle">锁句柄</param>
    /// <param name="expiry">新的失效时长</param>
    /// <param name="cancellationToken">取消续期等待的令牌</param>
    /// <returns>锁仍归当前句柄持有且续期成功时返回 true</returns>
    Task<bool> RenewAsync(IDistributedLockHandle lockHandle, TimeSpan expiry, CancellationToken cancellationToken = default);

}
