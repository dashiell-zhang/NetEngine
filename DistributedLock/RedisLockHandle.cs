using StackExchange.Redis;
using System;

namespace DistributedLock
{
    public class RedisLockHandle : IDisposable
    {

        public IDatabase Database { get; set; }


        public string LockKey { get; set; }


        public void Dispose()
        {
            try
            {
                Database.LockRelease(LockKey, "123456");
            }
            catch
            {

            }

            GC.SuppressFinalize(this);

        }
    }
}
