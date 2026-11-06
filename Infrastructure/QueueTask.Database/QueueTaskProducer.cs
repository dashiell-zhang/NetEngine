using Common;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using QueueTask.Database.Models;
using System.Transactions;

namespace QueueTask.Database;

/// <summary>
/// 将任务固定写入指定业务上下文，复用业务事务或独立提交
/// </summary>
public sealed class QueueTaskProducer<TContext>(TContext db, IDbContextFactory<TContext> factory, IdService idService) : IQueueTaskProducer where TContext : DbContext
{

    /// <summary>
    /// 在同一业务上下文添加任务，不保存或提交调用方事务
    /// </summary>
    public long Create(string name, object? parameter, DateTimeOffset? planTime = null, string? callbackName = null, object? callbackParameter = null, long? parentTaskId = null)
    {

        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("事务入队需要所属业务上下文已经开启显式事务");
        }

        var task = CreateRecord(name, parameter, planTime, callbackName, callbackParameter, parentTaskId);
        db.Set<Models.QueueTask>().Add(task);
        return task.Id;

    }


    /// <summary>
    /// 在新上下文独立保存，失败直接向调用方报告异常
    /// </summary>
    public async Task<long> CreateSingleAsync(string name, object? parameter, DateTimeOffset? planTime = null, string? callbackName = null, object? callbackParameter = null, long? parentTaskId = null, CancellationToken cancellationToken = default)
    {

        using var suppressed = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var task = CreateRecord(name, parameter, planTime, callbackName, callbackParameter, parentTaskId);
        context.Set<Models.QueueTask>().Add(task);
        await context.SaveChangesAsync(cancellationToken);
        return task.Id;

    }


    /// <summary>
    /// 统一校验并序列化入队参数，父任务仅属于当前模块
    /// </summary>
    private Models.QueueTask CreateRecord(string name, object? parameter, DateTimeOffset? planTime, string? callbackName, object? callbackParameter, long? parentTaskId)
    {

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (callbackParameter is not null && string.IsNullOrWhiteSpace(callbackName))
        {
            throw new ArgumentException("回调参数需要指定有效回调名称", nameof(callbackName));
        }
        if (planTime < DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(planTime), "计划时间不能早于当前时间");
        }

        return new Models.QueueTask
        {
            Id = idService.GetId(),
            Name = name,
            Status = QueueTaskStatus.Pending,
            Parameter = parameter is null ? null : JsonHelper.ObjectCloneJson(parameter),
            PlanTime = planTime,
            CallbackName = callbackName,
            CallbackParameter = callbackParameter is null ? null : JsonHelper.ObjectCloneJson(callbackParameter),
            ParentTaskId = parentTaskId
        };

    }

}
