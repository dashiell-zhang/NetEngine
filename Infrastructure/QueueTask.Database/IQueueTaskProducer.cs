namespace QueueTask.Database;

/// <summary>
/// 为选定任务执行模块提供入队能力
/// </summary>
public interface IQueueTaskProducer
{

    /// <summary>
    /// 在所属业务上下文的显式事务中添加任务，由调用方保存
    /// </summary>
    long Create(string name, object? parameter, DateTimeOffset? planTime = null, string? callbackName = null, object? callbackParameter = null, long? parentTaskId = null);


    /// <summary>
    /// 隔离环境事务后在所属业务库独立保存任务
    /// </summary>
    Task<long> CreateSingleAsync(string name, object? parameter, DateTimeOffset? planTime = null, string? callbackName = null, object? callbackParameter = null, long? parentTaskId = null, CancellationToken cancellationToken = default);

}
