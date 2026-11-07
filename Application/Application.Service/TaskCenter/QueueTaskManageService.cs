using Application.Interface.TaskCenter;
using Application.Model.Shared;
using Application.Model.TaskCenter;
using Microsoft.Extensions.DependencyInjection;
using global::QueueTask.Database;
using Repository;
using SourceGenerator.Runtime.Attributes;

namespace Application.Service.TaskCenter;

/// <summary>
/// 将本业务队列管理结果转换为管理端契约
/// </summary>
[RegisterService(typeof(IQueueTaskManageService), Lifetime = ServiceLifetime.Scoped)]
public class QueueTaskManageService(QueueTaskManagement<DatabaseContext> management) : IQueueTaskManageService
{

    /// <summary>
    /// 查询本业务队列并转换分页结果
    /// </summary>
    public async Task<PageListDto<QueueTaskDto>> GetQueueTaskListAsync(QueueTaskPageRequestDto request)
    {

        var result = await management.QueryTasksAsync(request.Skip(), request.PageSize, request.IsSuccess, request.StartCreateTime, request.EndCreateTime, request.ParameterKeyword);
        return new PageListDto<QueueTaskDto>
        {
            Total = result.Total,
            List = result.Items.Select(t => new QueueTaskDto
            {
                Id = t.Id,
                Name = t.Name,
                Status = (int)t.Status,
                WorkerId = t.WorkerId,
                Parameter = t.Parameter,
                PlanTime = t.PlanTime,
                Count = t.Count,
                FirstTime = t.FirstTime,
                LastTime = t.LastTime,
                SuccessTime = t.SuccessTime,
                LeaseExpireTime = t.LeaseExpireTime,
                CallbackName = t.CallbackName,
                CallbackParameter = t.CallbackParameter,
                ParentTaskId = t.ParentTaskId,
                ChildSuccessTime = t.ChildSuccessTime,
                CreateTime = t.CreateTime
            }).ToList()
        };

    }


    /// <summary>
    /// 请求重试本业务未成功且没有有效执行租约的任务
    /// </summary>
    public async Task<bool> RetryQueueTaskAsync(long id)
    {

        await management.RetryAsync(id);
        return true;

    }

}
