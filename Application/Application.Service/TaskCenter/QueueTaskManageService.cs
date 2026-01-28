using Application.Model.Shared;
using Application.Model.TaskCenter;
using Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository;
using SourceGenerator.Runtime.Attributes;

namespace Application.Service.TaskCenter;

[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class QueueTaskManageService(DatabaseContext db)
{

    /// <summary>
    /// 获取队列任务列表
    /// </summary>
    public async Task<PageListDto<QueueTaskDto>> GetQueueTaskListAsync(QueueTaskPageRequestDto request)
    {
        PageListDto<QueueTaskDto> result = new();

        var query = db.QueueTask.AsNoTracking().AsQueryable();

        // 添加检索条件
        if (request.IsSuccess != null)
        {
            query = request.IsSuccess.Value
                ? query.Where(t => t.SuccessTime != null)
                : query.Where(t => t.SuccessTime == null);
        }

        if (request.StartCreateTime.HasValue)
        {
            query = query.Where(t => t.CreateTime >= request.StartCreateTime.Value);
        }

        if (request.EndCreateTime.HasValue)
        {
            query = query.Where(t => t.CreateTime <= request.EndCreateTime.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.ParameterKeyword))
        {
            query = query.Where(t => t.Parameter != null && t.Parameter.Contains(request.ParameterKeyword));
        }

        result.Total = await query.CountAsync();

        if (result.Total != 0)
        {
            result.List = await query.OrderByDescending(t => t.Id).Select(t => new QueueTaskDto
            {
                Id = t.Id,
                Name = t.Name,
                Parameter = t.Parameter,
                PlanTime = t.PlanTime,
                Count = t.Count,
                FirstTime = t.FirstTime,
                LastTime = t.LastTime,
                SuccessTime = t.SuccessTime,
                CallbackName = t.CallbackName,
                CallbackParameter = t.CallbackParameter,
                ParentTaskId = t.ParentTaskId,
                ChildSuccessTime = t.ChildSuccessTime,
                CreateTime = t.CreateTime
            }).Skip(request.Skip()).Take(request.PageSize).ToListAsync();
        }

        return result;
    }


    /// <summary>
    /// 重试队列任务（仅限未成功的任务）
    /// </summary>
    public async Task<bool> RetryQueueTaskAsync(long id)
    {
        var task = await db.QueueTask.Where(t => t.Id == id).FirstOrDefaultAsync();

        if (task == null)
        {
            throw new CustomException("无效的 QueueTaskId");
        }

        if (task.SuccessTime != null)
        {
            throw new CustomException("已成功的任务不允许重试");
        }

        task.PlanTime = null;
        task.Count = 0;
        task.FirstTime = null;
        task.LastTime = null;
        task.SuccessTime = null;
        task.ChildSuccessTime = null;

        await db.SaveChangesAsync();

        return true;
    }
}
