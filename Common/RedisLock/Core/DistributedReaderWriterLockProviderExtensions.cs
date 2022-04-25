// 自动生成
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.RedisLock.Core
{
    /// <summary>
    /// <see cref="IDistributedReaderWriterLockProvider" /> 的生产力辅助方法
    /// </summary>
    public static class DistributedReaderWriterLockProviderExtensions
    {
        /// <summary>
        /// 相当于调用 <see cref="IDistributedReaderWriterLockProvider.CreateReaderWriterLock(string)" /> 然后
        /// <参见 cref="IDistributedReaderWriterLock.TryAcquireReadLock(TimeSpan, CancellationToken)" />。
        /// </summary>
        public static IDistributedSynchronizationHandle? TryAcquireReadLock(this IDistributedReaderWriterLockProvider provider, string name, TimeSpan timeout = default, CancellationToken cancellationToken = default) =>
            (provider ?? throw new ArgumentNullException(nameof(provider))).CreateReaderWriterLock(name).TryAcquireReadLock(timeout, cancellationToken);

        /// <summary>
        /// 相当于调用 <see cref="IDistributedReaderWriterLockProvider.CreateReaderWriterLock(string)" /> 然后
        /// <参见 cref="IDistributedReaderWriterLock.AcquireReadLock(TimeSpan?, CancellationToken)" />。
        /// </summary>
        public static IDistributedSynchronizationHandle AcquireReadLock(this IDistributedReaderWriterLockProvider provider, string name, TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            (provider ?? throw new ArgumentNullException(nameof(provider))).CreateReaderWriterLock(name).AcquireReadLock(timeout, cancellationToken);

        /// <summary>
        /// 相当于调用 <see cref="IDistributedReaderWriterLockProvider.CreateReaderWriterLock(string)" /> 然后
        /// <参见 cref="IDistributedReaderWriterLock.TryAcquireReadLockAsync(TimeSpan, CancellationToken)" />。
        /// </summary>
        public static ValueTask<IDistributedSynchronizationHandle?> TryAcquireReadLockAsync(this IDistributedReaderWriterLockProvider provider, string name, TimeSpan timeout = default, CancellationToken cancellationToken = default) =>
            (provider ?? throw new ArgumentNullException(nameof(provider))).CreateReaderWriterLock(name).TryAcquireReadLockAsync(timeout, cancellationToken);

        /// <summary>
        /// 相当于调用 <see cref="IDistributedReaderWriterLockProvider.CreateReaderWriterLock(string)" /> 然后
        /// <参见 cref="IDistributedReaderWriterLock.AcquireReadLockAsync(TimeSpan?, CancellationToken)" />。
        /// </summary>
        public static ValueTask<IDistributedSynchronizationHandle> AcquireReadLockAsync(this IDistributedReaderWriterLockProvider provider, string name, TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            (provider ?? throw new ArgumentNullException(nameof(provider))).CreateReaderWriterLock(name).AcquireReadLockAsync(timeout, cancellationToken);

        /// <summary>
        /// 相当于调用 <see cref="IDistributedReaderWriterLockProvider.CreateReaderWriterLock(string)" /> 然后
        /// <参见 cref="IDistributedReaderWriterLock.TryAcquireWriteLock(TimeSpan, CancellationToken)" />。
        /// </summary>
        public static IDistributedSynchronizationHandle? TryAcquireWriteLock(this IDistributedReaderWriterLockProvider provider, string name, TimeSpan timeout = default, CancellationToken cancellationToken = default) =>
            (provider ?? throw new ArgumentNullException(nameof(provider))).CreateReaderWriterLock(name).TryAcquireWriteLock(timeout, cancellationToken);

        /// <summary>
        /// 相当于调用 <see cref="IDistributedReaderWriterLockProvider.CreateReaderWriterLock(string)" /> 然后
        /// <参见 cref="IDistributedReaderWriterLock.AcquireWriteLock(TimeSpan?, CancellationToken)" />。
        /// </summary>
        public static IDistributedSynchronizationHandle AcquireWriteLock(this IDistributedReaderWriterLockProvider provider, string name, TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            (provider ?? throw new ArgumentNullException(nameof(provider))).CreateReaderWriterLock(name).AcquireWriteLock(timeout, cancellationToken);

        /// <summary>
        /// 相当于调用 <see cref="IDistributedReaderWriterLockProvider.CreateReaderWriterLock(string)" /> 然后
        /// <参见 cref="IDistributedReaderWriterLock.TryAcquireWriteLockAsync(TimeSpan, CancellationToken)" />。
        /// </summary>
        public static ValueTask<IDistributedSynchronizationHandle?> TryAcquireWriteLockAsync(this IDistributedReaderWriterLockProvider provider, string name, TimeSpan timeout = default, CancellationToken cancellationToken = default) =>
            (provider ?? throw new ArgumentNullException(nameof(provider))).CreateReaderWriterLock(name).TryAcquireWriteLockAsync(timeout, cancellationToken);

        /// <summary>
        /// 相当于调用 <see cref="IDistributedReaderWriterLockProvider.CreateReaderWriterLock(string)" /> 然后
        /// <参见 cref="IDistributedReaderWriterLock.AcquireWriteLockAsync(TimeSpan?, CancellationToken)" />。
        /// </summary>
        public static ValueTask<IDistributedSynchronizationHandle> AcquireWriteLockAsync(this IDistributedReaderWriterLockProvider provider, string name, TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            (provider ?? throw new ArgumentNullException(nameof(provider))).CreateReaderWriterLock(name).AcquireWriteLockAsync(timeout, cancellationToken);
    }
}