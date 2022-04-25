using Common.RedisLock.Core.Internal;
using Common.RedisLock.Primitives;
using Common.RedisLock.RedLock;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Common.RedisLock
{
    /// <summary>
    /// 使用 Redis 实现 <see cref="IDistributedLock"/>。 可以通过 RedLock 算法利用多个服务器。
    /// </summary>
    public sealed partial class RedisDistributedLock : IInternalDistributedLock<RedisDistributedLockHandle>
    {
        private readonly IReadOnlyList<IDatabase> _databases;
        private readonly RedisDistributedLockOptions _options;

        /// <summary>
        /// 使用提供的 <paramref name="database"/> 和 <paramref name="options"/> 构造一个名为 <paramref name="key"/> 的锁。
        /// </summary>
        public RedisDistributedLock(RedisKey key, IDatabase database, Action<RedisDistributedSynchronizationOptionsBuilder>? options = null)
            : this(key, new[] { database ?? throw new ArgumentNullException(nameof(database)) }, options)
        {
        }

        /// <summary>
        /// 使用提供的 <paramref name="databases"/> 和 <paramref name="options"/> 构造一个名为 <paramref name="key"/> 的锁。
        /// </summary>
        public RedisDistributedLock(RedisKey key, IEnumerable<IDatabase> databases, Action<RedisDistributedSynchronizationOptionsBuilder>? options = null)
        {
            if (key == default(RedisKey)) { throw new ArgumentNullException(nameof(key)); }
            this._databases = ValidateDatabases(databases);

            this.Key = key;
            this._options = RedisDistributedSynchronizationOptionsBuilder.GetOptions(options);
        }

        internal static IReadOnlyList<IDatabase> ValidateDatabases(IEnumerable<IDatabase> databases)
        {
            var databasesArray = databases?.ToArray() ?? throw new ArgumentNullException(nameof(databases));
            if (databasesArray.Length == 0) { throw new ArgumentException("may not be empty", nameof(databases)); }
            if (databasesArray.Contains(null!)) { throw new ArgumentNullException(nameof(databases), "may not contain null"); }
            return databasesArray;
        }

        /// <summary>
        /// 用于实现锁的 Redis key
        /// </summary>
        public RedisKey Key { get; }

        /// <summary>
        /// 实现 <see cref="IDistributedLock.Name"/>
        /// </summary>
        public string Name => this.Key.ToString();

        ValueTask<RedisDistributedLockHandle?> IInternalDistributedLock<RedisDistributedLockHandle>.InternalTryAcquireAsync(TimeoutValue timeout, CancellationToken cancellationToken) =>
            BusyWaitHelper.WaitAsync(
                state: this,
                tryGetValue: (@this, cancellationToken) => @this.TryAcquireAsync(cancellationToken),
                timeout: timeout,
                minSleepTime: this._options.MinBusyWaitSleepTime,
                maxSleepTime: this._options.MaxBusyWaitSleepTime,
                cancellationToken: cancellationToken
            );

        private async ValueTask<RedisDistributedLockHandle?> TryAcquireAsync(CancellationToken cancellationToken)
        {
            var primitive = new RedisMutexPrimitive(this.Key, RedLockHelper.CreateLockId(), this._options.RedLockTimeouts);
            var tryAcquireTasks = await new RedLockAcquire(primitive, this._databases, cancellationToken).TryAcquireAsync().ConfigureAwait(false);
            return tryAcquireTasks != null
                ? new RedisDistributedLockHandle(new RedLockHandle(primitive, tryAcquireTasks, extensionCadence: this._options.ExtensionCadence, expiry: this._options.RedLockTimeouts.Expiry))
                : null;
        }
    }
}
