using StackExchange.Redis;

namespace DistributedLock.Redis
{
    public sealed class RedisLockHandle : IDisposable
    {

        public IDatabase Database { get; set; }


        public string LockKey { get; set; }

        public void Dispose()
        {
            Database.LockReleaseAsync(LockKey, "123456");
        }

    }
}
