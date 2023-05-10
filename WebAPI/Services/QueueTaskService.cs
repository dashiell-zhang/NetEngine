using Common;
using Repository.Database;

namespace QueueTask
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


        public bool Create(string action, object? parameter)
        {
            TQueueTask queueTask = new()
            {
                Id = idHelper.GetId(),
                CreateTime = DateTime.UtcNow,
                Action = action
            };

            if (parameter != null)
            {
                queueTask.Parameter = JsonHelper.ObjectToJson(parameter);
            }

            db.TQueueTask.Add(queueTask);

            db.SaveChanges();

            return false;
        }
    }
}
