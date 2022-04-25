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
    /// 使用 Redis 实现 <see cref="IDistributedSemaphore"/>。
    /// </summary>
    public sealed partial class RedisDistributedSemaphore : IInternalDistributedSemaphore<RedisDistributedSemaphoreHandle>
    {
        /// <summary>
        /// 注意：虽然我们将其存储为列表以简化与 RedLock 组件的交互，但实际上是信号量
        /// 算法仅适用于单个数据库。 对于多个数据库，我们可能会违反我们的 <see cref="MaxCount"/>。
        /// 例如，3个dbs和2个ticket，我们可以有3个用户分别获得AB、BC和AC。 每个数据库都看到 2 张票！
        /// </summary>
        private readonly IReadOnlyList<IDatabase> _databases;
        private readonly RedisDistributedLockOptions _options;

        /// <summary>
        /// 使用提供的 <paramref name="maxCount"/>、<paramref name="database"/> 和 <paramref name="options"/> 构造一个名为 <paramref name="key"/> 的信号量。
        /// </summary>
        public RedisDistributedSemaphore(RedisKey key, int maxCount, IDatabase database, Action<RedisDistributedSynchronizationOptionsBuilder>? options = null)
        {
            if (key == default(RedisKey)) { throw new ArgumentNullException(nameof(key)); }
            if (maxCount < 1) { throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount, "must be positive"); }
            this._databases = new[] { database ?? throw new ArgumentNullException(nameof(database)) };

            this.Key = key;
            this.MaxCount = maxCount;
            this._options = RedisDistributedSynchronizationOptionsBuilder.GetOptions(options);
        }

        internal RedisKey Key { get; }

        /// <summary>
        /// 实现 <see cref="IDistributedSemaphore.Name"/>
        /// </summary>
        public string Name => this.Key.ToString();

        /// <summary>
        /// 实现 <see cref="IDistributedSemaphore.MaxCount"/>
        /// </summary>
        public int MaxCount { get; }

        ValueTask<RedisDistributedSemaphoreHandle?> IInternalDistributedSemaphore<RedisDistributedSemaphoreHandle>.InternalTryAcquireAsync(TimeoutValue timeout, CancellationToken cancellationToken) =>
            BusyWaitHelper.WaitAsync(
                state: this,
                tryGetValue: (@this, cancellationToken) => @this.TryAcquireAsync(cancellationToken),
                timeout: timeout,
                minSleepTime: this._options.MinBusyWaitSleepTime,
                maxSleepTime: this._options.MaxBusyWaitSleepTime,
                cancellationToken: cancellationToken
            );

        private async ValueTask<RedisDistributedSemaphoreHandle?> TryAcquireAsync(CancellationToken cancellationToken)
        {
            var primitive = new RedisSemaphorePrimitive(this.Key, this.MaxCount, this._options.RedLockTimeouts);
            var tryAcquireTasks = await new RedLockAcquire(primitive, this._databases, cancellationToken).TryAcquireAsync().ConfigureAwait(false);
            return tryAcquireTasks != null
                ? new RedisDistributedSemaphoreHandle(new RedLockHandle(primitive, tryAcquireTasks, extensionCadence: this._options.ExtensionCadence, expiry: this._options.RedLockTimeouts.Expiry))
                : null;
        }
    }
}
