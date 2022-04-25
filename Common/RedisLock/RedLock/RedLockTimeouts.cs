using Common.RedisLock.Core.Internal;

namespace Common.RedisLock.RedLock
{
    internal readonly struct RedLockTimeouts
    {
        public RedLockTimeouts(
            TimeoutValue expiry,
            TimeoutValue minValidityTime)
        {
            this.Expiry = expiry;
            this.MinValidityTime = minValidityTime;
        }

        public TimeoutValue Expiry { get; }
        public TimeoutValue MinValidityTime { get; }
        public TimeoutValue AcquireTimeout => this.Expiry.TimeSpan - this.MinValidityTime.TimeSpan;
    }
}
