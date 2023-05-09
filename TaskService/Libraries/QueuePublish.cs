using Common;
using Repository.Database;

namespace TaskService.Libraries
{

    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class QueuePublish
    {

        private readonly DatabaseContext db;
        private readonly IDHelper idHelper;

        public QueuePublish(DatabaseContext db, IDHelper idHelper)
        {
            this.db = db;
            this.idHelper = idHelper;
        }


        public bool Publish(string action, object? parameter)
        {

            TQueueTask queueTask = new();
            queueTask.Id = idHelper.GetId();
            queueTask.CreateTime = DateTime.UtcNow;
            queueTask.Action = action;

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
