using Application.Core.Interfaces.Basic;
using Common;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;

namespace Application.Core.Services.Basic
{

    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class TaskService(DatabaseContext db, IDbContextFactory<DatabaseContext> dbFactory, IdService idService) : ITaskService
    {


        public bool Create(string name, object? parameter, DateTimeOffset? planTime = null, string? callbackName = null, object? callbackParameter = null)
        {

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new Exception("name 为非法参数");
            }

            if (callbackParameter != null && string.IsNullOrWhiteSpace(callbackName))
            {
                throw new Exception("callbackName 为非法参数");
            }

            if (planTime < DateTimeOffset.UtcNow)
            {
                throw new Exception("planTime 为非法参数");
            }

            if (db.Database.CurrentTransaction != null)
            {
                TQueueTask queueTask = new()
                {
                    Id = idService.GetId(),
                    Name = name,
                    PlanTime = planTime,
                    CallbackName = callbackName,
                };

                if (parameter != null)
                {
                    queueTask.Parameter = JsonHelper.ObjectCloneJson(parameter);
                }

                if (callbackName != null && callbackParameter != null)
                {
                    queueTask.CallbackParameter = JsonHelper.ObjectCloneJson(callbackParameter);
                }

                db.TQueueTask.Add(queueTask);

                return true;
            }
            else
            {
                throw new Exception("请开启一个显式的事务");
            }
        }




        public async Task<bool> CreateSingleAsync(string name, object? parameter, DateTimeOffset? planTime = null, string? callbackName = null, object? callbackParameter = null)
        {

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new Exception("name 为非法参数");
            }

            if (callbackParameter != null && string.IsNullOrWhiteSpace(callbackName))
            {
                throw new Exception("callbackName 为非法参数");
            }

            if (planTime < DateTimeOffset.UtcNow)
            {
                throw new Exception("planTime 为非法参数");
            }

            try
            {
                var db = await dbFactory.CreateDbContextAsync();

                TQueueTask queueTask = new()
                {
                    Id = idService.GetId(),
                    Name = name,
                    PlanTime = planTime,
                    CallbackName = callbackName,
                };

                if (parameter != null)
                {
                    queueTask.Parameter = JsonHelper.ObjectCloneJson(parameter);
                }

                if (callbackName != null && callbackParameter != null)
                {
                    queueTask.CallbackParameter = JsonHelper.ObjectCloneJson(callbackParameter);
                }

                db.TQueueTask.Add(queueTask);

                await db.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
