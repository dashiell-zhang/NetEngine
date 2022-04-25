using Common.RedisLock.Core.Internal;
using Common.RedisLock.RedLock;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace Common.RedisLock.Primitives
{
    /// <summary>
    /// 信号量算法看起来类似于互斥体实现，只是存储在键处的值是
    /// 排序集（按超时排序）。 因为元素在超时时不会自动从集合中删除，
    /// 潜在的收购者必须首先清除所有过期值的集合，然后再检查该集合是否有空间
    /// 为他们。
    /// </summary>
    internal class RedisSemaphorePrimitive : IRedLockAcquirableSynchronizationPrimitive, IRedLockExtensibleSynchronizationPrimitive
    {
        // 在调用非确定性函数之前需要调用replicate_commands
        private const string GetNowMillisScriptFragment = @"
            redis.replicate_commands()
            local nowResult = redis.call('time')
            local nowMillis = (tonumber(nowResult[1]) * 1000.0) + (tonumber(nowResult[2]) / 1000.0)";

        private const string RenewSetScriptFragment = @"
            local keyTtl = redis.call('pttl', @key)
            if keyTtl < tonumber(@setExpiryMillis) then
                redis.call('pexpire', @key, @setExpiryMillis)
            end";

        private readonly RedisValue _lockId = RedLockHelper.CreateLockId();
        private readonly RedisKey _key;
        private readonly int _maxCount;
        private readonly RedLockTimeouts _timeouts;

        public RedisSemaphorePrimitive(RedisKey key, int maxCount, RedLockTimeouts timeouts)
        {
            this._key = key;
            this._maxCount = maxCount;
            this._timeouts = timeouts;
        }

        public TimeoutValue AcquireTimeout => this._timeouts.AcquireTimeout;

        /// <summary>
        /// 实际到期时间由超时集合中的条目决定。 但是，我们也不想通过离开来污染数据库
        /// 永远存在的集合。 因此，我们给集合的到期时间是单个条目到期时间的 3 倍。 额外的理由
        /// 对集合的保守之处在于，丢失它们比键超时造成的干扰更大。
        /// </summary>
        private TimeoutValue SetExpiry => TimeSpan.FromMilliseconds((int)Math.Min(int.MaxValue, 3L * this._timeouts.Expiry.InMilliseconds));

        public void Release(IDatabase database, bool fireAndForget) =>
            database.SortedSetRemove(this._key, this._lockId, RedLockHelper.GetCommandFlags(fireAndForget));

        public Task ReleaseAsync(IDatabaseAsync database, bool fireAndForget) =>
            database.SortedSetRemoveAsync(this._key, this._lockId, RedLockHelper.GetCommandFlags(fireAndForget));

        private static readonly RedisScript<RedisSemaphorePrimitive> AcquireScript = new RedisScript<RedisSemaphorePrimitive>($@"
            {GetNowMillisScriptFragment}
            redis.call('zremrangebyscore', @key, '-inf', nowMillis)
            if redis.call('zcard', @key) < tonumber(@maxCount) then
                redis.call('zadd', @key, nowMillis + tonumber(@expiryMillis), @lockId)
                {RenewSetScriptFragment}
                return 1
            end
            return 0",
            p => new { key = p._key, maxCount = p._maxCount, expiryMillis = p._timeouts.Expiry.InMilliseconds, lockId = p._lockId, setExpiryMillis = p.SetExpiry.InMilliseconds }
        );

        public bool TryAcquire(IDatabase database) => (bool)AcquireScript.Execute(database, this);

        public Task<bool> TryAcquireAsync(IDatabaseAsync database) => AcquireScript.ExecuteAsync(database, this).AsBooleanTask();

        private static readonly RedisScript<RedisSemaphorePrimitive> ExtendScript = new RedisScript<RedisSemaphorePrimitive>($@"
            {GetNowMillisScriptFragment}
            local result = redis.call('zadd', @key, 'XX', 'CH', nowMillis + tonumber(@expiryMillis), @lockId)
            {RenewSetScriptFragment}
            return result",
            p => new { key = p._key, expiryMillis = p._timeouts.Expiry.InMilliseconds, lockId = p._lockId, setExpiryMillis = p.SetExpiry.InMilliseconds }
        );

        public Task<bool> TryExtendAsync(IDatabaseAsync database) => ExtendScript.ExecuteAsync(database, this).AsBooleanTask();
    }
}
