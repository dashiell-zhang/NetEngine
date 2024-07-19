using Common;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Repository.Database;

namespace WebAPI.Services
{

    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class QueueTaskService(DatabaseContext db, IDbContextFactory<DatabaseContext> dbFactory, IdService idService)
    {


        /// <summary>
        /// 创建队列
        /// </summary>
        /// <param name="name"></param>
        /// <param name="parameter"></param>
        /// <param name="planTime"></param>
        /// <param name="callbackName"></param>
        /// <param name="callbackParameter"></param>
        /// <remarks>需要外部开启事务</remarks>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public bool Create(string name, object? parameter, DateTimeOffset? planTime = null, string? callbackName = null, object? callbackParameter = null)
        {
            if (db.Database.CurrentTransaction != null)
            {
                return CreateSingle(name, parameter, planTime, callbackName, callbackParameter);
            }
            else
            {
                throw new Exception("请开启一个显式的事务");
            }
        }



        /// <summary>
        /// 单独创建队列
        /// </summary>
        /// <param name="name"></param>
        /// <param name="parameter"></param>
        /// <param name="planTime"></param>
        /// <param name="callbackName"></param>
        /// <param name="callbackParameter"></param>
        /// <returns></returns>
        public bool CreateSingle(string name, object? parameter, DateTimeOffset? planTime = null, string? callbackName = null, object? callbackParameter = null)
        {
            try
            {
                var db = dbFactory.CreateDbContext();

                TQueueTask queueTask = new()
                {
                    Id = idService.GetId(),
                    Name = name,
                    PlanTime = planTime,
                    CallbackName = callbackName,
                };

                if (parameter != null)
                {
                    queueTask.Parameter = JsonHelper.ObjectToJson(parameter);
                }

                if (callbackName != null && callbackParameter != null)
                {
                    queueTask.CallbackParameter = JsonHelper.ObjectToJson(callbackParameter);
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

    }
}
