using Common.RedisLock.Core;
using Common.RedisLock.Core.Internal;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.RedisLock
{
    public partial class RedisDistributedReaderWriterLock
    {
        // 自动生成

        IDistributedSynchronizationHandle? IDistributedReaderWriterLock.TryAcquireReadLock(TimeSpan timeout, CancellationToken cancellationToken) =>
            this.TryAcquireReadLock(timeout, cancellationToken);
        IDistributedSynchronizationHandle IDistributedReaderWriterLock.AcquireReadLock(TimeSpan? timeout, CancellationToken cancellationToken) =>
            this.AcquireReadLock(timeout, cancellationToken);
        ValueTask<IDistributedSynchronizationHandle?> IDistributedReaderWriterLock.TryAcquireReadLockAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
            this.TryAcquireReadLockAsync(timeout, cancellationToken).Convert(To<IDistributedSynchronizationHandle?>.ValueTask);
        ValueTask<IDistributedSynchronizationHandle> IDistributedReaderWriterLock.AcquireReadLockAsync(TimeSpan? timeout, CancellationToken cancellationToken) =>
            this.AcquireReadLockAsync(timeout, cancellationToken).Convert(To<IDistributedSynchronizationHandle>.ValueTask);
        IDistributedSynchronizationHandle? IDistributedReaderWriterLock.TryAcquireWriteLock(TimeSpan timeout, CancellationToken cancellationToken) =>
            this.TryAcquireWriteLock(timeout, cancellationToken);
        IDistributedSynchronizationHandle IDistributedReaderWriterLock.AcquireWriteLock(TimeSpan? timeout, CancellationToken cancellationToken) =>
            this.AcquireWriteLock(timeout, cancellationToken);
        ValueTask<IDistributedSynchronizationHandle?> IDistributedReaderWriterLock.TryAcquireWriteLockAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
            this.TryAcquireWriteLockAsync(timeout, cancellationToken).Convert(To<IDistributedSynchronizationHandle?>.ValueTask);
        ValueTask<IDistributedSynchronizationHandle> IDistributedReaderWriterLock.AcquireWriteLockAsync(TimeSpan? timeout, CancellationToken cancellationToken) =>
            this.AcquireWriteLockAsync(timeout, cancellationToken).Convert(To<IDistributedSynchronizationHandle>.ValueTask);

        /// <summary>
        /// 尝试同步获取 READ 锁。 允许多个阅读器。 与 WRITE 锁不兼容。 用法：
        /// <code>
        ///     using (var handle = myLock.TryAcquireReadLock(...))
        ///     {
        ///         if (handle != null) { /* we have the lock! */ }
        ///     }
        ///     // dispose releases the lock if we took it
        /// </code>
        /// </summary>
        /// <param name="timeout">在放弃获取尝试之前等待多长时间。 默认为 0</param>
        /// <param name="cancellationToken">指定可以取消等待的令牌</param>
        /// <returns>一个 <see cref="RedisDistributedReaderWriterLockHandle"/> 可用于释放锁或失败时为空</returns>
        public RedisDistributedReaderWriterLockHandle? TryAcquireReadLock(TimeSpan timeout = default, CancellationToken cancellationToken = default) =>
            DistributedLockHelpers.TryAcquire(this, timeout, cancellationToken, isWrite: false);

        /// <summary>
        /// 同步获取 READ 锁，如果尝试超时，则失败并返回 <see cref="TimeoutException"/>。 允许多个阅读器。 与 WRITE 锁不兼容。 用法：
        /// <code>
        ///     using (myLock.AcquireReadLock(...))
        ///     {
        ///         /* we have the lock! */
        ///     }
        ///     // dispose releases the lock
        /// </code>
        /// </summary>
        /// <param name="timeout">在放弃获取尝试之前等待多长时间。 默认为 <see cref="Timeout.InfiniteTimeSpan"/></param>
        /// <param name="cancellationToken">指定可以取消等待的令牌</param>
        /// <returns>一个<see cref="RedisDistributedReaderWriterLockHandle"/>可以用来释放锁</returns>
        public RedisDistributedReaderWriterLockHandle AcquireReadLock(TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            DistributedLockHelpers.Acquire(this, timeout, cancellationToken, isWrite: false);

        /// <summary>
        /// 尝试异步获取 READ 锁。 允许多个阅读器。 与 WRITE 锁不兼容。 用法：
        /// <code>
        ///     await using (var handle = await myLock.TryAcquireReadLockAsync(...))
        ///     {
        ///         if (handle != null) { /* we have the lock! */ }
        ///     }
        ///     // dispose releases the lock if we took it
        /// </code>
        /// </summary>
        /// <param name="timeout">在放弃获取尝试之前等待多长时间。 默认为 0</param>
        /// <param name="cancellationToken">指定可以取消等待的令牌</param>
        /// <returns>一个 <see cref="RedisDistributedReaderWriterLockHandle"/> 可用于释放锁或失败时为空</returns>
        public ValueTask<RedisDistributedReaderWriterLockHandle?> TryAcquireReadLockAsync(TimeSpan timeout = default, CancellationToken cancellationToken = default) =>
            this.As<IInternalDistributedReaderWriterLock<RedisDistributedReaderWriterLockHandle>>().InternalTryAcquireAsync(timeout, cancellationToken, isWrite: false);

        /// <summary>
        /// 异步获取 READ 锁，如果尝试超时，则失败并返回 <see cref="TimeoutException"/>。 允许多个阅读器。 与 WRITE 锁不兼容。 用法：
        /// <code>
        ///     await using (await myLock.AcquireReadLockAsync(...))
        ///     {
        ///         /* we have the lock! */
        ///     }
        ///     // dispose releases the lock
        /// </code>
        /// </summary>
        /// <param name="timeout">在放弃获取尝试之前等待多长时间。 默认为 <see cref="Timeout.InfiniteTimeSpan"/></param>
        /// <param name="cancellationToken">指定可以取消等待的令牌</param>
        /// <returns>一个<see cref="RedisDistributedReaderWriterLockHandle"/>可以用来释放锁</returns>
        public ValueTask<RedisDistributedReaderWriterLockHandle> AcquireReadLockAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            DistributedLockHelpers.AcquireAsync(this, timeout, cancellationToken, isWrite: false);

        /// <summary>
        /// 尝试同步获取 WRITE 锁。 与另一个 WRITE 锁或 UPGRADE 锁不兼容。 用法：
        /// <code>
        ///     using (var handle = myLock.TryAcquireWriteLock(...))
        ///     {
        ///         if (handle != null) { /* we have the lock! */ }
        ///     }
        ///     // dispose releases the lock if we took it
        /// </code>
        /// </summary>
        /// <param name="timeout">在放弃获取尝试之前等待多长时间。 默认为 0</param>
        /// <param name="cancellationToken">指定可以取消等待的令牌</param>
        /// <returns>一个 <see cref="RedisDistributedReaderWriterLockHandle"/> 可用于释放锁或失败时为空</returns>
        public RedisDistributedReaderWriterLockHandle? TryAcquireWriteLock(TimeSpan timeout = default, CancellationToken cancellationToken = default) =>
            DistributedLockHelpers.TryAcquire(this, timeout, cancellationToken, isWrite: true);

        /// <summary>
        /// 同步获取 WRITE 锁，如果尝试超时，则失败并返回 <see cref="TimeoutException"/>。 与另一个 WRITE 锁或 UPGRADE 锁不兼容。 用法：
        /// <code>
        ///     using (myLock.AcquireWriteLock(...))
        ///     {
        ///         /* we have the lock! */
        ///     }
        ///     // dispose releases the lock
        /// </code>
        /// </summary>
        /// <param name="timeout">在放弃获取尝试之前等待多长时间。 默认为 <see cref="Timeout.InfiniteTimeSpan"/></param>
        /// <param name="cancellationToken">指定可以取消等待的令牌</param>
        /// <returns>一个<see cref="RedisDistributedReaderWriterLockHandle"/>可以用来释放锁</returns>
        public RedisDistributedReaderWriterLockHandle AcquireWriteLock(TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            DistributedLockHelpers.Acquire(this, timeout, cancellationToken, isWrite: true);

        /// <summary>
        /// 尝试异步获取 WRITE 锁。 与另一个 WRITE 锁或 UPGRADE 锁不兼容。 用法：
        /// <code>
        ///     await using (var handle = await myLock.TryAcquireWriteLockAsync(...))
        ///     {
        ///         if (handle != null) { /* we have the lock! */ }
        ///     }
        ///     // dispose releases the lock if we took it
        /// </code>
        /// </summary>
        /// <param name="timeout">在放弃获取尝试之前等待多长时间。 默认为 0</param>
        /// <param name="cancellationToken">指定可以取消等待的令牌</param>
        /// <returns>一个 <see cref="RedisDistributedReaderWriterLockHandle"/> 可用于释放锁或失败时为空</returns>
        public ValueTask<RedisDistributedReaderWriterLockHandle?> TryAcquireWriteLockAsync(TimeSpan timeout = default, CancellationToken cancellationToken = default) =>
            this.As<IInternalDistributedReaderWriterLock<RedisDistributedReaderWriterLockHandle>>().InternalTryAcquireAsync(timeout, cancellationToken, isWrite: true);

        /// <summary>
        /// 异步获取 WRITE 锁，如果尝试超时，则失败并返回 <see cref="TimeoutException"/>。 与另一个 WRITE 锁或 UPGRADE 锁不兼容。 用法：
        /// <code>
        ///     await using (await myLock.AcquireWriteLockAsync(...))
        ///     {
        ///         /* we have the lock! */
        ///     }
        ///     // dispose releases the lock
        /// </code>
        /// </summary>
        /// <param name="timeout">在放弃获取尝试之前等待多长时间。 默认为 <see cref="Timeout.InfiniteTimeSpan"/></param>
        /// <param name="cancellationToken">指定可以取消等待的令牌</param>
        /// <returns>一个<see cref="RedisDistributedReaderWriterLockHandle"/>可以用来释放锁</returns>
        public ValueTask<RedisDistributedReaderWriterLockHandle> AcquireWriteLockAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            DistributedLockHelpers.AcquireAsync(this, timeout, cancellationToken, isWrite: true);

    }
}