using System;

namespace Common.DistributedLock
{
    public class RedisLockHandle : IDisposable
    {

        public string LockKey { get; set; }

        public void Dispose()
        {
            try
            {
                RedisHelper.UnLock(LockKey, "123456");
            }
            catch
            {

            }
        }
    }
}
