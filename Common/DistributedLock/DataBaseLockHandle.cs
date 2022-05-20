using Repository.Database;
using System;
using System.Linq;

namespace Common.DistributedLock
{
    public class DataBaseLockHandle : IDisposable
    {

        public string LockKey { get; set; }

        public void Dispose()
        {
            try
            {
                using (DatabaseContext db = new())
                {
                    var lk = db.TLock.Where(t => t.IsDelete == false && t.Id == LockKey).FirstOrDefault();

                    if (lk != null)
                    {
                        db.TLock.Remove(lk);
                        db.SaveChanges();
                    }
                }
            }
            catch
            {

            }
        }
    }
}
