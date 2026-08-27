using SourceGenerator.Runtime.Pipeline;

namespace SourceGenerator.Runtime;

/// <summary>
/// 提供代理类在运行时执行行为管道的统一入口
/// </summary>
public static class ProxyRuntime
{

    /// <summary>
    /// 在同步调用场景下执行同步行为管道并返回结果
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="ctx">调用上下文</param>
    /// <param name="inner">实际执行目标方法的同步委托</param>
    /// <returns>目标方法最终返回值</returns>
    public static T Execute<T>(InvocationContext ctx, Func<T> inner)
    {

        var behaviors = ctx.Behaviors;
        ValidateSynchronousBehaviors(behaviors);

        try
        {
            for (var i = 0; i < behaviors.Count; i++)
            {
                ((IInvocationBehavior)behaviors[i]).OnBefore(ctx);
            }

            var result = inner();

            for (var i = 0; i < behaviors.Count; i++)
            {
                ((IInvocationBehavior)behaviors[i]).OnAfter(ctx, result);
            }

            return result;
        }
        catch (Exception ex)
        {
            for (var i = 0; i < behaviors.Count; i++)
            {
                ((IInvocationBehavior)behaviors[i]).OnException(ctx, ex);
            }

            throw;
        }

    }


    /// <summary>
    /// 在 ValueTask 异步调用场景下执行行为管道
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="ctx">调用上下文</param>
    /// <param name="inner">实际执行目标方法的异步委托</param>
    /// <returns>封装目标方法返回值的 ValueTask</returns>
    public static ValueTask<T> ExecuteAsync<T>(InvocationContext ctx, Func<ValueTask<T>> inner)
    {
        return InvocationPipeline.ExecuteAsync<T>(ctx, inner, ctx.Behaviors);
    }


    /// <summary>
    /// 在 Task 异步调用场景下执行行为管道
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="ctx">调用上下文</param>
    /// <param name="inner">实际执行目标方法的 Task 异步委托</param>
    /// <returns>封装目标方法返回值的 Task</returns>
    public static Task<T> ExecuteAsync<T>(InvocationContext ctx, Func<Task<T>> inner)
    {

        return InvocationPipeline
            .ExecuteAsync<T>(ctx, () => AdaptTask(inner), ctx.Behaviors)
            .AsTask();

    }


    /// <summary>
    /// 在 Task 无返回值的异步调用场景下执行行为管道
    /// </summary>
    /// <param name="ctx">调用上下文</param>
    /// <param name="inner">实际执行目标方法的 Task 异步委托</param>
    /// <returns>表示调用完成的 Task</returns>
    public static Task ExecuteTask(InvocationContext ctx, Func<Task> inner)
    {
        return InvocationPipeline
            .ExecuteAsync<object?>(ctx, async () => { await inner().ConfigureAwait(false); return null; }, ctx.Behaviors)
            .AsTask();
    }


    /// <summary>
    /// 在 ValueTask 无返回值的异步调用场景下执行行为管道
    /// </summary>
    /// <param name="ctx">调用上下文</param>
    /// <param name="inner">实际执行目标方法的 ValueTask 异步委托</param>
    public static async ValueTask ExecuteTask(InvocationContext ctx, Func<ValueTask> inner)
    {
        await InvocationPipeline
            .ExecuteAsync<object?>(ctx, async () => { await inner().ConfigureAwait(false); return null; }, ctx.Behaviors)
            .ConfigureAwait(false);
    }


    /// <summary>
    /// 将 Task 调用适配为 ValueTask 并保留同步异常和取消状态
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="inner">实际执行目标方法的 Task 异步委托</param>
    /// <returns>封装目标方法执行状态的 ValueTask</returns>
    private static ValueTask<T> AdaptTask<T>(Func<Task<T>> inner)
    {

        try
        {
            return new ValueTask<T>(inner());
        }
        catch (OperationCanceledException ex)
        {
            var completionSource = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            completionSource.SetCanceled(ex.CancellationToken);
            return new ValueTask<T>(completionSource.Task);
        }
        catch (Exception ex)
        {
            return ValueTask.FromException<T>(ex);
        }

    }


    /// <summary>
    /// 验证当前调用中的全部行为均可用于同步代理路径
    /// </summary>
    /// <param name="behaviors">待验证的行为列表</param>
    private static void ValidateSynchronousBehaviors(IReadOnlyList<IInvocationAsyncBehavior> behaviors)
    {

        for (var i = 0; i < behaviors.Count; i++)
        {
            if (behaviors[i] is not IInvocationBehavior)
            {
                throw new InvalidOperationException($"行为 {behaviors[i].GetType().FullName} 不支持同步代理方法，请将目标方法返回类型改为 Task 或 ValueTask");
            }
        }

    }

}
