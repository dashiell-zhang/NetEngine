using System;
using System.Threading;

namespace Common.DistributedLock
{
    public class RedisLock : IDistributedLock
    {




        /// <summary>
        /// 获取锁
        /// </summary>
        /// <param name="key">锁的名称，不可重复</param>
        /// <param name="expiry">失效时长</param>
        /// <param name="semaphore">信号量</param>
        /// <returns></returns>
        public IDisposable Lock(string key, TimeSpan expiry = default, int semaphore = 1)
        {

            if (expiry == default)
            {
                expiry = TimeSpan.FromMinutes(1);
            }

            var endTime = DateTime.UtcNow + expiry;

            RedisLockHandle redisLockHandle = new();

        StartTag:
            {
                for (int i = 0; i < semaphore; i++)
                {
                    var keyMd5 = CryptoHelper.GetMD5(key + i);

                    try
                    {
                        if (RedisHelper.Lock(keyMd5, "123456", expiry))
                        {

                            redisLockHandle.LockKey = keyMd5;
                            return redisLockHandle;
                        }
                    }
                    catch
                    {

                    }
                }


                if (redisLockHandle.LockKey == default)
                {

                    if (DateTime.UtcNow < endTime)
                    {
                        Thread.Sleep(1000);
                        goto StartTag;
                    }
                    else
                    {
                        throw new Exception("获取锁" + key + "超时失败");
                    }
                }
            }

            return redisLockHandle;
        }




        public IDisposable? TryLock(string key, TimeSpan expiry = default, int semaphore = 1)
        {

            if (expiry == default)
            {
                expiry = TimeSpan.FromMinutes(1);
            }


            for (int i = 0; i < semaphore; i++)
            {
                var keyMd5 = CryptoHelper.GetMD5(key + i);

                try
                {
                    if (RedisHelper.Lock(keyMd5, "123456", expiry))
                    {
                        RedisLockHandle redisLockHandle = new()
                        {
                            LockKey = keyMd5
                        };
                        return redisLockHandle;
                    }
                }
                catch
                {

                }
            }
            return null;

        }
    }
}
