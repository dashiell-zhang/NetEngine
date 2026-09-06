using Microsoft.Extensions.Logging;

namespace SourceGenerator.Runtime.Pipeline;

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
        var enteredBehaviorCount = 0;

        try
        {
            InvokeSynchronousBefore(ctx, ref enteredBehaviorCount);

            var result = inner();

            InvokeSynchronousAfter(ctx, ref enteredBehaviorCount, result);

            return result;
        }
        catch (Exception ex)
        {
            InvokeSynchronousException(ctx, ref enteredBehaviorCount, ex);

            throw;
        }

    }


    /// <summary>
    /// 按声明顺序进入同步行为并记录包括当前行为在内的已进入数量
    /// </summary>
    public static void InvokeSynchronousBefore(InvocationContext ctx, ref int enteredBehaviorCount)
    {

        while (enteredBehaviorCount < ctx.Behaviors.Count)
        {
            var behavior = (IInvocationBehavior)ctx.Behaviors[enteredBehaviorCount++];
            behavior.OnBefore(ctx);
        }

    }


    /// <summary>
    /// 按进入顺序的逆序结束同步行为 仅在成功结束后移出当前行为
    /// </summary>
    public static void InvokeSynchronousAfter(InvocationContext ctx, ref int enteredBehaviorCount, object? result)
    {

        while (enteredBehaviorCount > 0)
        {
            ((IInvocationBehavior)ctx.Behaviors[enteredBehaviorCount - 1]).OnAfter(ctx, result);
            enteredBehaviorCount--;
        }

    }


    /// <summary>
    /// 逆序通知尚未结束的同步行为 保留原始异常并继续处理其余回调
    /// </summary>
    public static void InvokeSynchronousException(InvocationContext ctx, ref int enteredBehaviorCount, Exception exception)
    {

        while (enteredBehaviorCount > 0)
        {
            var behavior = (IInvocationBehavior)ctx.Behaviors[--enteredBehaviorCount];
            try
            {
                behavior.OnException(ctx, exception);
            }
            catch (Exception callbackException)
            {
                try
                {
                    ctx.Logger?.LogError(callbackException, "同步行为异常回调失败 已保留原始异常 method={Method} behavior={Behavior}", ctx.Method, behavior.GetType().FullName);
                }
                catch
                {
                    // 日志提供程序失败不能覆盖原始业务异常或中断其余回调
                }
            }
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
        return InvocationPipeline.ExecuteAsync<T>(ctx, inner);
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
            .ExecuteAsync<T>(ctx, () => AdaptTask(inner))
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
            .ExecuteAsync<object?>(ctx, async () => { await inner().ConfigureAwait(false); return null; })
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
            .ExecuteAsync<object?>(ctx, async () => { await inner().ConfigureAwait(false); return null; })
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
