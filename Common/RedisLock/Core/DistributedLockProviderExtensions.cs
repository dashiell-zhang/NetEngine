// 自动生成
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.RedisLock.Core
{
    /// <summary>
    /// <see cref="IDistributedLockProvider" /> 的生产力辅助方法
    /// </summary>
    public static class DistributedLockProviderExtensions
    {
        /// <summary>
        /// 相当于调用 <see cref="IDistributedLockProvider.CreateLock(string)" /> 然后
        /// <参见 cref="IDistributedLock.TryAcquire(TimeSpan, CancellationToken)" />。
        /// </summary>
        public static IDistributedSynchronizationHandle? TryAcquireLock(this IDistributedLockProvider provider, string name, TimeSpan timeout = default, CancellationToken cancellationToken = default) =>
            (provider ?? throw new ArgumentNullException(nameof(provider))).CreateLock(name).TryAcquire(timeout, cancellationToken);

        /// <summary>
        /// 相当于调用 <see cref="IDistributedLockProvider.CreateLock(string)" /> 然后
        /// <see cref="IDistributedLock.Acquire(TimeSpan?, CancellationToken)" />。
        /// </summary>
        public static IDistributedSynchronizationHandle AcquireLock(this IDistributedLockProvider provider, string name, TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            (provider ?? throw new ArgumentNullException(nameof(provider))).CreateLock(name).Acquire(timeout, cancellationToken);

        /// <summary>
        /// 相当于调用 <see cref="IDistributedLockProvider.CreateLock(string)" /> 然后
        /// <参见 cref="IDistributedLock.TryAcquireAsync(TimeSpan, CancellationToken)" />。
        /// </summary>
        public static ValueTask<IDistributedSynchronizationHandle?> TryAcquireLockAsync(this IDistributedLockProvider provider, string name, TimeSpan timeout = default, CancellationToken cancellationToken = default) =>
            (provider ?? throw new ArgumentNullException(nameof(provider))).CreateLock(name).TryAcquireAsync(timeout, cancellationToken);

        /// <summary>
        /// 相当于调用 <see cref="IDistributedLockProvider.CreateLock(string)" /> 然后
        /// <参见 cref="IDistributedLock.AcquireAsync(TimeSpan?, CancellationToken)" />。
        /// </summary>
        public static ValueTask<IDistributedSynchronizationHandle> AcquireLockAsync(this IDistributedLockProvider provider, string name, TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            (provider ?? throw new ArgumentNullException(nameof(provider))).CreateLock(name).AcquireAsync(timeout, cancellationToken);
    }
}