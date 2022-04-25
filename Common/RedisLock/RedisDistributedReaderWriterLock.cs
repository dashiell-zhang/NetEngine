using Common.RedisLock.Core.Internal;
using Common.RedisLock.Primitives;
using Common.RedisLock.RedLock;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Common.RedisLock
{
    /// <summary>
    /// 使用 Redis 实现 <see cref="IDistributedReaderWriterLock"/>。 可以通过 RedLock 算法利用多个服务器。
    /// </summary>
    public sealed partial class RedisDistributedReaderWriterLock : IInternalDistributedReaderWriterLock<RedisDistributedReaderWriterLockHandle>
    {
        private readonly IReadOnlyList<IDatabase> _databases;
        private readonly RedisDistributedLockOptions _options;

        /// <summary>
        /// 使用提供的 <paramref name="database"/> 和 <paramref name="options"/> 构造一个名为 <paramref name="name"/> 的锁。
        /// </summary>
        public RedisDistributedReaderWriterLock(string name, IDatabase database, Action<RedisDistributedSynchronizationOptionsBuilder>? options = null)
            : this(name, new[] { database ?? throw new ArgumentNullException(nameof(database)) }, options)
        {
        }

        /// <summary>
        /// 使用提供的 <paramref name="databases"/> 和 <paramref name="options"/> 构造一个名为 <paramref name="name"/> 的锁。
        /// </summary>
        public RedisDistributedReaderWriterLock(string name, IEnumerable<IDatabase> databases, Action<RedisDistributedSynchronizationOptionsBuilder>? options = null)
        {
            if (name == null) { throw new ArgumentNullException(nameof(name)); }
            this._databases = RedisDistributedLock.ValidateDatabases(databases);

            this.ReaderKey = name + ".readers";
            this.WriterKey = name + ".writer";
            this.Name = name;
            this._options = RedisDistributedSynchronizationOptionsBuilder.GetOptions(options);

            // 我们坚持这条规则以确保当我们获取写入等待锁时，它不会在两次尝试之间过期
            // 将其升级为写锁。 这避免了延长写入器等待锁的需要
            if (this._options.RedLockTimeouts.MinValidityTime.CompareTo(this._options.MaxBusyWaitSleepTime) <= 0)
            {
                throw new ArgumentException($"{nameof(RedisDistributedSynchronizationOptionsBuilder.BusyWaitSleepTime)} must be <= {nameof(RedisDistributedSynchronizationOptionsBuilder.MinValidityTime)}", nameof(options));
            }
        }

        internal RedisKey ReaderKey { get; }
        internal RedisKey WriterKey { get; }

        /// <summary>
        /// 实现 <see cref="IDistributedReaderWriterLock.Name"/>
        /// </summary>
        public string Name { get; }

        ValueTask<RedisDistributedReaderWriterLockHandle?> IInternalDistributedReaderWriterLock<RedisDistributedReaderWriterLockHandle>.InternalTryAcquireAsync(
            TimeoutValue timeout,
            CancellationToken cancellationToken,
            bool isWrite)
        {
            return isWrite
                ? this.TryAcquireWriteLockAsync(timeout, cancellationToken)
                : BusyWaitHelper.WaitAsync(
                    this,
                    (@lock, cancellationToken) => @lock.TryAcquireAsync(new RedisReadLockPrimitive(@lock.ReaderKey, @lock.WriterKey, @lock._options.RedLockTimeouts), cancellationToken),
                    timeout: timeout,
                    minSleepTime: this._options.MinBusyWaitSleepTime,
                    maxSleepTime: this._options.MaxBusyWaitSleepTime,
                    cancellationToken
                );
        }

        private async ValueTask<RedisDistributedReaderWriterLockHandle?> TryAcquireWriteLockAsync(TimeoutValue timeout, CancellationToken cancellationToken)
        {
            var acquireWriteLockState = new AcquireWriteLockState(canRetry: !timeout.IsZero);
            RedisDistributedReaderWriterLockHandle? handle = null;
            try
            {
                return handle = await BusyWaitHelper.WaitAsync(
                   (Lock: this, State: acquireWriteLockState),
                   (state, cancellationToken) => state.Lock.TryAcquireWriteLockAsync(state.State, cancellationToken),
                   timeout: timeout,
                   minSleepTime: this._options.MinBusyWaitSleepTime,
                   maxSleepTime: this._options.MaxBusyWaitSleepTime,
                   cancellationToken
                ).ConfigureAwait(false);
            }
            finally
            {
                // 如果我们没有获得写锁但是我们获得了写等待锁，释放
                // 作家等待我们退出的锁。
                if (handle == null && acquireWriteLockState.WriterWaiting.TryGetValue(out var writerWaiting))
                {
                    await new RedLockRelease(writerWaiting.Primitive, writerWaiting.TryAcquireTasks).ReleaseAsync().ConfigureAwait(false);
                }
            }
        }

        private async ValueTask<RedisDistributedReaderWriterLockHandle?> TryAcquireWriteLockAsync(AcquireWriteLockState state, CancellationToken cancellationToken)
        {
            // 第一次，通过，只是尝试获取写锁。 这涵盖了 TryAcquire(0) 的情况，并确保我们
            // 如果我们不需要，不要打扰编写器等待锁。
            if (state.IsFirstTry)
            {
                state.IsFirstTry = false;
                var firstTryResult = await TryAcquireWriteLockAsync(RedLockHelper.CreateLockId()).ConfigureAwait(false);
                if (firstTryResult != null) { return firstTryResult; }
                // 如果我们不打算重试获取，请不要费心尝试编写器等待锁
                if (!state.CanRetry) { return null; }
            }

            Invariant.Require(state.CanRetry);

            // 否则，如果我们还没有编写器等待锁，请尝试使用它
            if (!state.WriterWaiting.HasValue)
            {
                var lockId = RedLockHelper.CreateLockId();
                var primitive = new RedisWriterWaitingPrimitive(this.WriterKey, lockId, this._options.RedLockTimeouts);
                var tryAcquireTasks = await new RedLockAcquire(primitive, this._databases, cancellationToken).TryAcquireAsync().ConfigureAwait(false);
                if (tryAcquireTasks == null) { return null; }

                // 如果我们让作家等待，请保存信息并继续前进
                state.WriterWaiting = (primitive, tryAcquireTasks, lockId);
            }

            // 如果我们到达这里，我们就有了作家等待锁。 尝试将其“升级”为实际的写入器锁
            return await TryAcquireWriteLockAsync(state.WriterWaiting.Value.LockId).ConfigureAwait(false);

            ValueTask<RedisDistributedReaderWriterLockHandle?> TryAcquireWriteLockAsync(RedisValue lockId) =>
                this.TryAcquireAsync(new RedisWriteLockPrimitive(this.ReaderKey, this.WriterKey, lockId, this._options.RedLockTimeouts), cancellationToken);
        }

        private async ValueTask<RedisDistributedReaderWriterLockHandle?> TryAcquireAsync<TPrimitive>(TPrimitive primitive, CancellationToken cancellationToken)
            where TPrimitive : IRedLockAcquirableSynchronizationPrimitive, IRedLockExtensibleSynchronizationPrimitive
        {
            var tryAcquireTasks = await new RedLockAcquire(primitive, this._databases, cancellationToken).TryAcquireAsync().ConfigureAwait(false);
            return tryAcquireTasks != null
                ? new RedisDistributedReaderWriterLockHandle(new RedLockHandle(primitive, tryAcquireTasks, extensionCadence: this._options.ExtensionCadence, expiry: this._options.RedLockTimeouts.Expiry))
                : null;
        }

        private class AcquireWriteLockState
        {
            public AcquireWriteLockState(bool canRetry)
            {
                this.CanRetry = canRetry;
            }

            public bool CanRetry { get; }

            public bool IsFirstTry { get; set; } = true;

            public (RedisWriterWaitingPrimitive Primitive, IReadOnlyDictionary<IDatabase, Task<bool>> TryAcquireTasks, RedisValue LockId)? WriterWaiting { get; set; }
        }
    }
}
