// 自动生成
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.RedisLock.Core
{
    /// <summary>
    /// <see cref="IDistributedSemaphoreProvider" /> 的生产力辅助方法
    /// </summary>
    public static class DistributedSemaphoreProviderExtensions
    {
        /// <summary>
        /// 相当于调用 <see cref="IDistributedSemaphoreProvider.CreateSemaphore(string, int)" /> 然后
        /// <参见 cref="IDistributedSemaphore.TryAcquire(TimeSpan, CancellationToken)" />。
        /// </summary>
        public static IDistributedSynchronizationHandle? TryAcquireSemaphore(this IDistributedSemaphoreProvider provider, string name, int maxCount, TimeSpan timeout = default, CancellationToken cancellationToken = default) =>
            (provider ?? throw new ArgumentNullException(nameof(provider))).CreateSemaphore(name, maxCount).TryAcquire(timeout, cancellationToken);

        /// <summary>
        /// 相当于调用 <see cref="IDistributedSemaphoreProvider.CreateSemaphore(string, int)" /> 然后
        /// <see cref="IDistributedSemaphore.Acquire(TimeSpan?, CancellationToken)" />.
        /// </summary>
        public static IDistributedSynchronizationHandle AcquireSemaphore(this IDistributedSemaphoreProvider provider, string name, int maxCount, TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            (provider ?? throw new ArgumentNullException(nameof(provider))).CreateSemaphore(name, maxCount).Acquire(timeout, cancellationToken);

        /// <summary>
        /// 相当于调用 <see cref="IDistributedSemaphoreProvider.CreateSemaphore(string, int)" /> 然后
        /// <参见 cref="IDistributedSemaphore.TryAcquireAsync(TimeSpan, CancellationToken)" />。
        /// </summary>
        public static ValueTask<IDistributedSynchronizationHandle?> TryAcquireSemaphoreAsync(this IDistributedSemaphoreProvider provider, string name, int maxCount, TimeSpan timeout = default, CancellationToken cancellationToken = default) =>
            (provider ?? throw new ArgumentNullException(nameof(provider))).CreateSemaphore(name, maxCount).TryAcquireAsync(timeout, cancellationToken);

        /// <summary>
        /// 相当于调用 <see cref="IDistributedSemaphoreProvider.CreateSemaphore(string, int)" /> 然后
        /// <参见 cref="IDistributedSemaphore.AcquireAsync(TimeSpan?, CancellationToken)" />。
        /// </summary>
        public static ValueTask<IDistributedSynchronizationHandle> AcquireSemaphoreAsync(this IDistributedSemaphoreProvider provider, string name, int maxCount, TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            (provider ?? throw new ArgumentNullException(nameof(provider))).CreateSemaphore(name, maxCount).AcquireAsync(timeout, cancellationToken);
    }
}