using Common;
using Repository.Database;

namespace WebAPI.Services
{

    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class QueueTaskService
    {

        private readonly DatabaseContext db;
        private readonly IDHelper idHelper;

        public QueueTaskService(DatabaseContext db, IDHelper idHelper)
        {
            this.db = db;
            this.idHelper = idHelper;
        }


        public bool Create(string name, object? parameter, DateTimeOffset? planTime = null)
        {
            if (db.Database.CurrentTransaction != null)
            {
                try
                {
                    TQueueTask queueTask = new()
                    {
                        Id = idHelper.GetId(),
                        Name = name,
                        PlanTime = planTime
                    };

                    if (parameter != null)
                    {
                        queueTask.Parameter = JsonHelper.ObjectToJson(parameter);
                    }

                    db.TQueueTask.Add(queueTask);

                    db.SaveChanges();

                    return true;
                }
                catch
                {
                    return false;
                }
            }
            else
            {
                throw new Exception("请开启一个显式的事务");
            }
        }
    }
}
