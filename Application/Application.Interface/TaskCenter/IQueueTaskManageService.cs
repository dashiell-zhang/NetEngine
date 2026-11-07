using Application.Model.Shared;
using Application.Model.TaskCenter;

namespace Application.Interface.TaskCenter;

/// <summary>
/// 本业务队列查询与手工重试
/// </summary>
public interface IQueueTaskManageService
{

    /// <summary>
    /// 查询本业务队列并转换分页结果
    /// </summary>
    Task<PageListDto<QueueTaskDto>> GetQueueTaskListAsync(QueueTaskPageRequestDto request);


    /// <summary>
    /// 请求重试本业务未成功且没有有效执行租约的任务
    /// </summary>
    Task<bool> RetryQueueTaskAsync(long id);

}
