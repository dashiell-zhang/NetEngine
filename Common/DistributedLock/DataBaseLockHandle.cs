using Repository.Database;
using System;
using System.Linq;

namespace Common.DistributedLock
{
    public class DataBaseLockHandle : IDisposable
    {

        private readonly DatabaseContext db;

        public DataBaseLockHandle(DatabaseContext _db)
        {
            db = _db;
        }

        public string LockKey { get; set; }

        public void Dispose()
        {
            try
            {
                var lk = db.TLock.Where(t => t.IsDelete == false && t.Id == LockKey).FirstOrDefault();

                if (lk != null)
                {
                    db.TLock.Remove(lk);
                    db.SaveChanges();
                }
            }
            catch
            {

            }
        }
    }
}
