using Common;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;

namespace TaskService.Core.QueueTask
{

    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class QueueTaskService(DatabaseContext db, IDbContextFactory<DatabaseContext> dbFactory, IdService idService)
    {


        /// <summary>
        /// 当前TaskId
        /// </summary>
        public long? CurrentTaskId { get; set; }



        /// <summary>
        /// 创建队列
        /// </summary>
        /// <param name="name"></param>
        /// <param name="parameter"></param>
        /// <param name="planTime"></param>
        /// <param name="callbackName"></param>
        /// <param name="callbackParameter"></param>
        /// <param name="isChild"></param>
        /// <remarks>需要外部开启事务</remarks>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public bool Create(string name, object? parameter, DateTimeOffset? planTime = null, string? callbackName = null, object? callbackParameter = null, bool isChild = false)
        {
            if (db.Database.CurrentTransaction != null)
            {
                TQueueTask queueTask = new()
                {
                    Id = idService.GetId(),
                    Name = name,
                    Parameter = parameter != null ? JsonHelper.ObjectToJson(parameter) : null,
                    PlanTime = planTime,
                    CallbackName = callbackName,
                    CallbackParameter = callbackName != null ? callbackParameter != null ? JsonHelper.ObjectToJson(callbackParameter) : null : null,
                };

                if (isChild)
                {
                    if (CurrentTaskId != null)
                    {
                        queueTask.ParentTaskId = CurrentTaskId;
                    }
                    else
                    {
                        throw new Exception("CurrentTaskId 无有效信息，无法创建子队列");
                    }
                }

                db.TQueueTask.Add(queueTask);
                return true;
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
        /// <param name="isChild"></param>
        /// <returns></returns>
        public bool CreateSingle(string name, object? parameter, DateTimeOffset? planTime = null, string? callbackName = null, object? callbackParameter = null, bool isChild = false)
        {
            try
            {
                var db = dbFactory.CreateDbContext();

                TQueueTask queueTask = new()
                {
                    Id = idService.GetId(),
                    Name = name,
                    Parameter = parameter != null ? JsonHelper.ObjectToJson(parameter) : null,
                    PlanTime = planTime,
                    CallbackName = callbackName,
                    CallbackParameter = callbackName != null ? callbackParameter != null ? JsonHelper.ObjectToJson(callbackParameter) : null : null
                };

                if (isChild)
                {
                    if (CurrentTaskId != null)
                    {
                        queueTask.ParentTaskId = CurrentTaskId;
                    }
                    else
                    {
                        throw new Exception("CurrentTaskId 无有效信息，无法创建子队列");
                    }
                }

                db.TQueueTask.Add(queueTask);

                db.SaveChanges();

                return true;
            }
            catch
            {
                throw new Exception("创建队列异常");
            }
        }


    }
}
