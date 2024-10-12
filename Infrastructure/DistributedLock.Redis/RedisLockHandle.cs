using StackExchange.Redis;

namespace DistributedLock.Redis
{
    public class RedisLockHandle : IDisposable
    {

        public IDatabase Database { get; set; }


        public string LockKey { get; set; }


        public void Dispose()
        {
            Database.LockReleaseAsync(LockKey, "123456");
            GC.SuppressFinalize(this);
        }
    }
}
