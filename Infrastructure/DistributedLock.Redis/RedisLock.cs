using DistributedLock.Redis.Models;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;

namespace DistributedLock.Redis
{
    public class RedisLock : IDistributedLock
    {

        private readonly Lazy<Task<ConnectionMultiplexer>> connectionMultiplexer;


        private readonly RedisSetting redisSetting;


        public RedisLock(IOptionsMonitor<RedisSetting> config)
        {
            redisSetting = config.CurrentValue;

            connectionMultiplexer = new(async () => await ConnectionMultiplexer.ConnectAsync(redisSetting.Configuration));
        }


        public async Task<IDisposable> LockAsync(string key, TimeSpan expiry = default, int semaphore = 1)
        {

            if (expiry == default)
            {
                expiry = TimeSpan.FromMinutes(1);
            }

            var endTime = DateTime.UtcNow + expiry;

            RedisLockHandle redisLockHandle = new();

            var keyMd5 = redisSetting.InstanceName + Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(key)));

        StartTag:
            {
                for (int i = 0; i < semaphore; i++)
                {
                    var tempKey = keyMd5 + " " + i;

                    try
                    {
                        var database = (await connectionMultiplexer.Value).GetDatabase();

                        if (await database.LockTakeAsync(tempKey, "123456", expiry))
                        {
                            redisLockHandle.LockKey = tempKey;
                            redisLockHandle.Database = database;
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
                        await Task.Delay(100);
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


        public async Task<IDisposable?> TryLockAsync(string key, TimeSpan expiry = default, int semaphore = 1)
        {

            if (expiry == default)
            {
                expiry = TimeSpan.FromMinutes(1);
            }

            var keyMd5 = redisSetting.InstanceName + Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(key)));

            for (int i = 0; i < semaphore; i++)
            {
                var tempKey = keyMd5 + " " + i;

                try
                {
                    var database = (await connectionMultiplexer.Value).GetDatabase();

                    if (await database.LockTakeAsync(tempKey, "123456", expiry))
                    {
                        RedisLockHandle redisLockHandle = new()
                        {
                            LockKey = tempKey,
                            Database = database
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
