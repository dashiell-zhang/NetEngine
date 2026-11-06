using global::QueueTask.Database;

namespace TaskService.Core.QueueTask;

/// <summary>
/// 为当前 Worker 的执行作用域提供入队和同库子任务关联
/// </summary>
public class QueueTaskService(IQueueTaskProducer producer)
{

    /// <summary>
    /// 当前执行任务标识，仅用于所选任务模块
    /// </summary>
    public long? CurrentTaskId { get; set; }


    /// <summary>
    /// 在选定模块的业务事务中创建任务
    /// </summary>
    public long Create(string name, object? parameter, DateTimeOffset? planTime = null, string? callbackName = null, object? callbackParameter = null, bool isChild = false)
    {

        return producer.Create(name, parameter, planTime, callbackName, callbackParameter, GetParentId(isChild));

    }


    /// <summary>
    /// 在选定模块中独立保存任务，失败向外抛出
    /// </summary>
    public Task<long> CreateSingleAsync(string name, object? parameter, DateTimeOffset? planTime = null, string? callbackName = null, object? callbackParameter = null, bool isChild = false)
    {

        return producer.CreateSingleAsync(name, parameter, planTime, callbackName, callbackParameter, GetParentId(isChild));

    }


    /// <summary>
    /// 仅在明确创建子任务且当前存在执行身份时关联父任务
    /// </summary>
    private long? GetParentId(bool isChild)
    {

        return isChild ? CurrentTaskId ?? throw new InvalidOperationException("当前没有执行中的父任务") : null;

    }

}
