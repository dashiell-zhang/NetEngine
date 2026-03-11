using StackExchange.Redis;

namespace DistributedLock.Redis;
public sealed class RedisLockHandle : IDisposable
{
    public string LockValue { get; set; } = "123456";

    public IDatabase Database { get; set; }


    public string LockKey { get; set; }

    public void Dispose()
    {
        Database.LockReleaseAsync(LockKey, LockValue);
    }

}
