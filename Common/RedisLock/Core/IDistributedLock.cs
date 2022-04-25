using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.RedisLock.Core
{
    /// <summary>
    /// 互斥同步原语，可用于协调对资源或代码关键区域的访问
    /// 跨进程或系统。 锁的范围和功能取决于特定的实现
    /// </summary>
    public interface IDistributedLock
    {
        /// <summary>
        /// 唯一标识锁的名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 尝试同步获取锁。 用法：
        /// <code>
        ///     using (var handle = myLock.TryAcquire(...))
        ///     {
        ///         if (handle != null) { /* we have the lock! */ }
        ///     }
        ///     // dispose releases the lock if we took it
        /// </code>
        /// </summary>
        /// <param name="timeout">在放弃获取尝试之前等待多长时间。 默认为 0</param>
        /// <param name="cancellationToken">指定可以取消等待的令牌</param>
        /// <returns>一个 <see cref="IDistributedSynchronizationHandle"/> 可用于释放锁或在失败时为空</returns>
        IDistributedSynchronizationHandle? TryAcquire(TimeSpan timeout = default, CancellationToken cancellationToken = default);

        /// <summary>
        /// 同步获取锁，如果尝试超时，则失败并返回 <see cref="TimeoutException"/>。 用法：
        /// <code>
        ///     using (myLock.Acquire(...))
        ///     {
        ///         /* we have the lock! */
        ///     }
        ///     // dispose releases the lock
        /// </code>
        /// </summary>
        /// <param name="timeout">在放弃获取尝试之前等待多长时间。 默认为 <see cref="Timeout.InfiniteTimeSpan"/></param>
        /// <param name="cancellationToken">指定可以取消等待的令牌</param>
        /// <returns>一个<see cref="IDistributedSynchronizationHandle"/>可以用来释放锁</returns>
        IDistributedSynchronizationHandle Acquire(TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 尝试异步获取锁。 用法：
        /// <code>
        ///     await using (var handle = await myLock.TryAcquireAsync(...))
        ///     {
        ///         if (handle != null) { /* we have the lock! */ }
        ///     }
        ///     // dispose releases the lock if we took it
        /// </code>
        /// </summary>
        /// <param name="timeout">在放弃获取尝试之前等待多长时间。 默认为 0</param>
        /// <param name="cancellationToken">指定可以取消等待的令牌</param>
        /// <returns>一个 <see cref="IDistributedSynchronizationHandle"/> 可用于释放锁或在失败时为空</returns>
        ValueTask<IDistributedSynchronizationHandle?> TryAcquireAsync(TimeSpan timeout = default, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步获取锁，如果尝试超时，则失败并返回 <see cref="TimeoutException"/>。 用法：
        /// <code>
        ///     await using (await myLock.AcquireAsync(...))
        ///     {
        ///         /* we have the lock! */
        ///     }
        ///     // dispose releases the lock
        /// </code>
        /// </summary>
        /// <param name="timeout">在放弃获取尝试之前等待多长时间。 默认为 <see cref="Timeout.InfiniteTimeSpan"/></param>
        /// <param name="cancellationToken">指定可以取消等待的令牌</param>
        /// <returns>一个<see cref="IDistributedSynchronizationHandle"/>可以用来释放锁</returns>
        ValueTask<IDistributedSynchronizationHandle> AcquireAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    }
}
