using Common.RedisLock.Core;
using Common.RedisLock.Core.Internal;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.RedisLock
{
    public partial class RedisDistributedLock
    {
        // 自动生成

        IDistributedSynchronizationHandle? IDistributedLock.TryAcquire(TimeSpan timeout, CancellationToken cancellationToken) =>
            this.TryAcquire(timeout, cancellationToken);
        IDistributedSynchronizationHandle IDistributedLock.Acquire(TimeSpan? timeout, CancellationToken cancellationToken) =>
            this.Acquire(timeout, cancellationToken);
        ValueTask<IDistributedSynchronizationHandle?> IDistributedLock.TryAcquireAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
            this.TryAcquireAsync(timeout, cancellationToken).Convert(To<IDistributedSynchronizationHandle?>.ValueTask);
        ValueTask<IDistributedSynchronizationHandle> IDistributedLock.AcquireAsync(TimeSpan? timeout, CancellationToken cancellationToken) =>
            this.AcquireAsync(timeout, cancellationToken).Convert(To<IDistributedSynchronizationHandle>.ValueTask);

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
        /// <returns>一个 <see cref="RedisDistributedLockHandle"/> 可用于释放锁或在失败时为空</returns>
        public RedisDistributedLockHandle? TryAcquire(TimeSpan timeout = default, CancellationToken cancellationToken = default) =>
            DistributedLockHelpers.TryAcquire(this, timeout, cancellationToken);

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
        /// <returns>一个<see cref="RedisDistributedLockHandle"/>可以用来释放锁</returns>
        public RedisDistributedLockHandle Acquire(TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            DistributedLockHelpers.Acquire(this, timeout, cancellationToken);

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
        /// <returns>一个 <see cref="RedisDistributedLockHandle"/> 可用于释放锁或在失败时为空</returns>
        public ValueTask<RedisDistributedLockHandle?> TryAcquireAsync(TimeSpan timeout = default, CancellationToken cancellationToken = default) =>
            this.As<IInternalDistributedLock<RedisDistributedLockHandle>>().InternalTryAcquireAsync(timeout, cancellationToken);

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
        /// <returns>一个<see cref="RedisDistributedLockHandle"/>可以用来释放锁</returns>
        public ValueTask<RedisDistributedLockHandle> AcquireAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            DistributedLockHelpers.AcquireAsync(this, timeout, cancellationToken);
    }
}