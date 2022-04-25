using Common.RedisLock.Core;
using Common.RedisLock.Core.Internal;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.RedisLock
{
    public partial class RedisDistributedSemaphore
    {
        // 自动生成

        IDistributedSynchronizationHandle? IDistributedSemaphore.TryAcquire(TimeSpan timeout, CancellationToken cancellationToken) =>
            this.TryAcquire(timeout, cancellationToken);
        IDistributedSynchronizationHandle IDistributedSemaphore.Acquire(TimeSpan? timeout, CancellationToken cancellationToken) =>
            this.Acquire(timeout, cancellationToken);
        ValueTask<IDistributedSynchronizationHandle?> IDistributedSemaphore.TryAcquireAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
            this.TryAcquireAsync(timeout, cancellationToken).Convert(To<IDistributedSynchronizationHandle?>.ValueTask);
        ValueTask<IDistributedSynchronizationHandle> IDistributedSemaphore.AcquireAsync(TimeSpan? timeout, CancellationToken cancellationToken) =>
            this.AcquireAsync(timeout, cancellationToken).Convert(To<IDistributedSynchronizationHandle>.ValueTask);

        /// <summary>
        /// 尝试同步获取信号量票证。 用法：
        /// <code>
        ///     using (var handle = mySemaphore.TryAcquire(...))
        ///     {
        ///         if (handle != null) { /* we have the ticket! */ }
        ///     }
        ///     // dispose releases the ticket if we took it
        /// </code>
        /// </summary>
        /// <param name="timeout">在放弃获取尝试之前等待多长时间。 默认为 0</param>
        /// <param name="cancellationToken">指定可以取消等待的令牌</param>
        /// <returns>一个 <see cref="RedisDistributedSemaphoreHandle"/> 可用于释放票证或失败时为空</returns>
        public RedisDistributedSemaphoreHandle? TryAcquire(TimeSpan timeout = default, CancellationToken cancellationToken = default) =>
            DistributedLockHelpers.TryAcquire(this, timeout, cancellationToken);

        /// <summary>
        /// 同步获取信号量票证，如果尝试超时，则失败并返回 <see cref="TimeoutException"/>。 用法：
        /// <code>
        ///     using (mySemaphore.Acquire(...))
        ///     {
        ///         /* we have the ticket! */
        ///     }
        ///     // dispose releases the ticket
        /// </code>
        /// </summary>
        /// <param name="timeout">在放弃获取尝试之前等待多长时间。 默认为 <see cref="Timeout.InfiniteTimeSpan"/></param>
        /// <param name="cancellationToken">指定可以取消等待的令牌</param>
        /// <returns>一个 <see cref="RedisDistributedSemaphoreHandle"/> 可以用来释放票证</returns>
        public RedisDistributedSemaphoreHandle Acquire(TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            DistributedLockHelpers.Acquire(this, timeout, cancellationToken);

        /// <summary>
        /// 尝试异步获取信号量票证。 用法：
        /// <code>
        ///     await using (var handle = await mySemaphore.TryAcquireAsync(...))
        ///     {
        ///         if (handle != null) { /* we have the ticket! */ }
        ///     }
        ///     // dispose releases the ticket if we took it
        /// </code>
        /// </summary>
        /// <param name="timeout">在放弃获取尝试之前等待多长时间。 默认为 0</param>
        /// <param name="cancellationToken">指定可以取消等待的令牌</param>
        /// <returns>一个 <see cref="RedisDistributedSemaphoreHandle"/> 可用于释放票证或失败时为空</returns>
        public ValueTask<RedisDistributedSemaphoreHandle?> TryAcquireAsync(TimeSpan timeout = default, CancellationToken cancellationToken = default) =>
            this.As<IInternalDistributedSemaphore<RedisDistributedSemaphoreHandle>>().InternalTryAcquireAsync(timeout, cancellationToken);

        /// <summary>
        /// 异步获取信号量票证，如果尝试超时，则失败并返回 <see cref="TimeoutException"/>。 用法：
        /// <code>
        ///     await using (await mySemaphore.AcquireAsync(...))
        ///     {
        ///         /* we have the ticket! */
        ///     }
        ///     // dispose releases the ticket
        /// </code>
        /// </summary>
        /// <param name="timeout">在放弃获取尝试之前等待多长时间。 默认为 <see cref="Timeout.InfiniteTimeSpan"/></param>
        /// <param name="cancellationToken">指定可以取消等待的令牌</param>
        /// <returns>一个 <see cref="RedisDistributedSemaphoreHandle"/> 可以用来释放票证</returns>
        public ValueTask<RedisDistributedSemaphoreHandle> AcquireAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            DistributedLockHelpers.AcquireAsync(this, timeout, cancellationToken);
    }
}