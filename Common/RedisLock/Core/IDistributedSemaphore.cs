using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.RedisLock.Core
{
    /// <summary>
    /// 一种同步原语，它将对资源或代码的关键部分的访问限制为固定数量的并发线程/进程。
    /// 与 <see cref="Semaphore"/> 进行比较。
    /// </summary>
    public interface IDistributedSemaphore
    {
        /// <summary>
        /// 唯一标识信号量的名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 信号量可用的最大“票”数（即可以同时获取信号量的进程数）
        /// </summary>
        int MaxCount { get; }

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
        /// <returns>一个 <see cref="IDistributedSynchronizationHandle"/> 可用于释放票证或失败时为空</returns>
        IDistributedSynchronizationHandle? TryAcquire(TimeSpan timeout = default, CancellationToken cancellationToken = default);

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
        /// <returns>一个 <see cref="IDistributedSynchronizationHandle"/> 可以用来释放票证</returns>
        IDistributedSynchronizationHandle Acquire(TimeSpan? timeout = null, CancellationToken cancellationToken = default);

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
        /// <returns>一个 <see cref="IDistributedSynchronizationHandle"/> 可用于释放票证或失败时为空</returns>
        ValueTask<IDistributedSynchronizationHandle?> TryAcquireAsync(TimeSpan timeout = default, CancellationToken cancellationToken = default);

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
        /// <returns>一个 <see cref="IDistributedSynchronizationHandle"/> 可以用来释放票证</returns>
        ValueTask<IDistributedSynchronizationHandle> AcquireAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    }
}
