using SourceGenerator.Runtime.Pipeline;

namespace SourceGenerator.Runtime;

/// <summary>
/// 提供代理类在运行时执行行为管道的统一入口
/// </summary>
public static class ProxyRuntime
{

    /// <summary>
    /// 在同步调用场景下执行行为管道 并返回结果
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="ctx">调用上下文</param>
    /// <param name="inner">实际执行目标方法的委托 以 ValueTask 形式返回</param>
    /// <returns>目标方法最终返回值</returns>
    public static T Execute<T>(InvocationContext ctx, Func<ValueTask<T>> inner)
    {
        var behaviors = ctx.Behaviors ?? new IInvocationAsyncBehavior[] { new Pipeline.Behaviors.LoggingBehavior() };

        return InvocationPipeline.ExecuteAsync<T>(ctx, inner, behaviors).GetAwaiter().GetResult();
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
        var behaviors = ctx.Behaviors ?? new IInvocationAsyncBehavior[] { new Pipeline.Behaviors.LoggingBehavior() };

        return InvocationPipeline.ExecuteAsync<T>(ctx, inner, behaviors);
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
        var behaviors = ctx.Behaviors ?? new IInvocationAsyncBehavior[] { new Pipeline.Behaviors.LoggingBehavior() };

        return InvocationPipeline
            .ExecuteAsync<T>(ctx, async () => await inner().ConfigureAwait(false), behaviors)
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
        var behaviors = ctx.Behaviors ?? new IInvocationAsyncBehavior[] { new Pipeline.Behaviors.LoggingBehavior() };

        return InvocationPipeline
            .ExecuteAsync<object?>(ctx, async () => { await inner().ConfigureAwait(false); return null; }, behaviors)
            .AsTask();
    }


    /// <summary>
    /// 在 ValueTask 无返回值的异步调用场景下执行行为管道
    /// </summary>
    /// <param name="ctx">调用上下文</param>
    /// <param name="inner">实际执行目标方法的 ValueTask 异步委托</param>
    public static async ValueTask ExecuteTask(InvocationContext ctx, Func<ValueTask> inner)
    {
        var behaviors = ctx.Behaviors ?? new IInvocationAsyncBehavior[] { new Pipeline.Behaviors.LoggingBehavior() };

        await InvocationPipeline
            .ExecuteAsync<object?>(ctx, async () => { await inner().ConfigureAwait(false); return null; }, behaviors)
            .ConfigureAwait(false);
    }

}
