using Common.RedisLock.Core.Internal;
using Common.RedisLock.RedLock;
using StackExchange.Redis;
using System.Threading.Tasks;

namespace Common.RedisLock.Primitives
{
    internal class RedisReadLockPrimitive : IRedLockAcquirableSynchronizationPrimitive, IRedLockExtensibleSynchronizationPrimitive
    {
        private readonly RedisValue _lockId = RedLockHelper.CreateLockId();
        private readonly RedisKey _readerKey, _writerKey;
        private readonly RedLockTimeouts _timeouts;

        public RedisReadLockPrimitive(RedisKey readerKey, RedisKey writerKey, RedLockTimeouts timeouts)
        {
            this._readerKey = readerKey;
            this._writerKey = writerKey;
            this._timeouts = timeouts;
        }

        public TimeoutValue AcquireTimeout => this._timeouts.AcquireTimeout;

        /// <summary>
        /// 释放读
        ///
        /// 只需从读取器集中删除我们的 ID（如果它不存在则不存在或设置 DNE）
        /// </summary>
        private static readonly RedisScript<RedisReadLockPrimitive> ReleaseReadScript = new RedisScript<RedisReadLockPrimitive>(
            @"redis.call('srem', @readerKey, @lockId)",
            p => new { readerKey = p._readerKey, lockId = p._lockId }
        );

        public void Release(IDatabase database, bool fireAndForget) => ReleaseReadScript.Execute(database, this, fireAndForget);
        public Task ReleaseAsync(IDatabaseAsync database, bool fireAndForget) => ReleaseReadScript.ExecuteAsync(database, this, fireAndForget);

        /// <summary>
        /// 尝试扩展阅读
        ///
        /// 首先，检查读卡器集是否存在，我们的ID是否仍然是成员。 如果没有，我们就失败了。
        ///
        /// 然后，将读取器设置的 TTL 扩展为至少我们的到期时间（至少因为其他读取器可能会以更长的到期时间运行）
        /// </summary>
        private static readonly RedisScript<RedisReadLockPrimitive> TryExtendReadScript = new RedisScript<RedisReadLockPrimitive>(@"
            if redis.call('sismember', @readerKey, @lockId) == 0 then
                return 0
            end
            if redis.call('pttl', @readerKey) < tonumber(@expiryMillis) then
                redis.call('pexpire', @readerKey, @expiryMillis)
            end
            return 1",
            p => new { readerKey = p._readerKey, lockId = p._lockId, expiryMillis = p._timeouts.Expiry.InMilliseconds }
        );

        public Task<bool> TryExtendAsync(IDatabaseAsync database) => TryExtendReadScript.ExecuteAsync(database, this).AsBooleanTask();

        /// <summary>
        /// 尝试获取读取
        ///
        /// 首先，检查写入者锁定值：如果存在，则失败。
        ///
        /// 然后，将我们的 ID 添加到阅读器集，如果它不存在则创建它。 然后，延长 TTL
        /// 的读者至少设置为我们的到期时间。 返回成功。
        /// </summary>
        private static readonly RedisScript<RedisReadLockPrimitive> TryAcquireReadScript = new RedisScript<RedisReadLockPrimitive>($@"
            if redis.call('exists', @writerKey) == 1 then
                return 0
            end
            redis.call('sadd', @readerKey, @lockId)
            local readerTtl = redis.call('pttl', @readerKey)
            if readerTtl < tonumber(@expiryMillis) then
                redis.call('pexpire', @readerKey, @expiryMillis)
            end
            return 1",
            p => new { writerKey = p._writerKey, readerKey = p._readerKey, lockId = p._lockId, expiryMillis = p._timeouts.Expiry.InMilliseconds }
        );

        public Task<bool> TryAcquireAsync(IDatabaseAsync database) => TryAcquireReadScript.ExecuteAsync(database, this).AsBooleanTask();
        public bool TryAcquire(IDatabase database) => (bool)TryAcquireReadScript.Execute(database, this);
    }

    internal class RedisWriterWaitingPrimitive : RedisMutexPrimitive
    {
        public const string LockIdSuffix = "_WRITERWAITING";

        public RedisWriterWaitingPrimitive(RedisKey writerKey, RedisValue baseLockId, RedLockTimeouts timeouts)
            : base(writerKey, baseLockId + LockIdSuffix, timeouts)
        {
        }
    }

    internal class RedisWriteLockPrimitive : IRedLockAcquirableSynchronizationPrimitive, IRedLockExtensibleSynchronizationPrimitive
    {
        private readonly RedisKey _readerKey, _writerKey;
        private readonly RedisValue _lockId;
        private readonly RedLockTimeouts _timeouts;
        private readonly RedisMutexPrimitive _mutexPrimitive;

        public RedisWriteLockPrimitive(
            RedisKey readerKey,
            RedisKey writerKey,
            RedisValue lockId,
            RedLockTimeouts timeouts)
        {
            this._readerKey = readerKey;
            this._writerKey = writerKey;
            this._lockId = lockId;
            this._timeouts = timeouts;
            this._mutexPrimitive = new RedisMutexPrimitive(this._writerKey, this._lockId, this._timeouts);
        }

        public TimeoutValue AcquireTimeout => this._timeouts.AcquireTimeout;

        public void Release(IDatabase database, bool fireAndForget) => this._mutexPrimitive.Release(database, fireAndForget);
        public Task ReleaseAsync(IDatabaseAsync database, bool fireAndForget) => this._mutexPrimitive.ReleaseAsync(database, fireAndForget);

        /// <summary>
        /// 尝试获取写入
        ///
        /// 首先，检查 writerValue 是否存在。 如果是这样，则失败，除非它是我们的等待 ID。
        ///
        /// 然后，检查是否没有读者。 如果是这样，则将 writerValue 设置为我们的 ID 并返回成功。 如果没有，那么如果锁
        /// 让我们的等待 ID 重新到期（避免延长写入器等待锁的需要）。
        ///
        /// 最后返回失败。
        /// </summary>
        private static readonly RedisScript<RedisWriteLockPrimitive> TryAcquireWriteScript = new RedisScript<RedisWriteLockPrimitive>($@"
            local writerValue = redis.call('get', @writerKey)
            if writerValue == false or writerValue == @lockId .. '{RedisWriterWaitingPrimitive.LockIdSuffix}' then
                if redis.call('scard', @readerKey) == 0 then
                    redis.call('set', @writerKey, @lockId, 'px', @expiryMillis)
                    return 1
                end
                if writerValue ~= false then
                    redis.call('pexpire', @writerKey, @expiryMillis)
                end
            end
            return 0",
            p => new { writerKey = p._writerKey, readerKey = p._readerKey, lockId = p._lockId, expiryMillis = p._timeouts.Expiry.InMilliseconds }
        );

        public bool TryAcquire(IDatabase database) => (bool)TryAcquireWriteScript.Execute(database, this);
        public Task<bool> TryAcquireAsync(IDatabaseAsync database) => TryAcquireWriteScript.ExecuteAsync(database, this).AsBooleanTask();

        public Task<bool> TryExtendAsync(IDatabaseAsync database) => this._mutexPrimitive.TryExtendAsync(database);
    }
}
