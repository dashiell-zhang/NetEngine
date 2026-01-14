using Common;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository;
using Repository.Database;
using SourceGenerator.Runtime.Attributes;

namespace Application.Service.QueueTask;

[RegisterService(Lifetime = ServiceLifetime.Scoped)]
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
            Repository.Database.QueueTask queueTask = new()
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

            db.QueueTask.Add(queueTask);

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
    /// <returns></returns>
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

            Repository.Database.QueueTask queueTask = new()
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

            db.QueueTask.Add(queueTask);

            await db.SaveChangesAsync();

            return true;
        }
        catch
        {
            return false;
        }
    }

}
