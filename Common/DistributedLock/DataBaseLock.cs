using Repository.Database;
using System;
using System.Linq;
using System.Threading;
using System.Timers;

namespace Common.DistributedLock
{
    public class DataBaseLock : IDistributedLock
    {

        private readonly DatabaseContext db;

        public DataBaseLock(DatabaseContext _db)
        {
            db = _db;
        }



        public DataBaseLock()
        {
            var timer = new System.Timers.Timer(1000 * 1);
            timer.Elapsed += TimerElapsed;
            timer.Start();
        }


        public IDisposable Lock(string key, TimeSpan expiry = default, int semaphore = 1)
        {

            if (expiry == default)
            {
                expiry = TimeSpan.FromMinutes(1);
            }

            var endTime = DateTime.UtcNow + expiry;

            DataBaseLockHandle dataBaseLockHandle = new(db);

        StartTag:
            {

                for (int i = 0; i < semaphore; i++)
                {
                    var keyMd5 = CryptoHelper.GetMD5(key + i);

                    try
                    {
                        TLock lk = new()
                        {
                            Id = keyMd5,
                            TTL = expiry.TotalSeconds,
                            CreateTime = DateTime.UtcNow
                        };

                        db.TLock.Add(lk);


                        db.SaveChanges();


                        dataBaseLockHandle.LockKey = keyMd5;
                        return dataBaseLockHandle;
                    }
                    catch
                    {

                    }
                }

                if (dataBaseLockHandle.LockKey == default)
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

            return dataBaseLockHandle;
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

                    TLock lk = new()
                    {
                        Id = keyMd5,
                        TTL = expiry.TotalSeconds,
                        CreateTime = DateTime.UtcNow
                    };

                    db.TLock.Add(lk);

                    db.SaveChanges();

                    DataBaseLockHandle dataBaseLockHandle = new(db)
                    {
                        LockKey = keyMd5
                    };
                    return dataBaseLockHandle;
                }
                catch
                {

                }
            }

            return null;
        }



        private void TimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                var nowTime = DateTime.UtcNow;
                var lkList = db.TLock.Where(t => t.CreateTime.AddSeconds(t.TTL) < nowTime).ToList();

                db.TLock.RemoveRange(lkList);
                db.SaveChanges();
            }
            catch
            {

            }
        }
    }
}
