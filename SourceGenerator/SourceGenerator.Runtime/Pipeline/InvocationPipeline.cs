namespace SourceGenerator.Runtime.Pipeline;

/// <summary>
/// 提供构建并执行异步调用行为管道的辅助方法
/// </summary>
public static class InvocationPipeline
{

    /// <summary>
    /// 根据给定行为列表组装调用管道 并执行最终结果
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="ctx">调用上下文</param>
    /// <param name="inner">最终实际执行目标方法的委托</param>
    /// <param name="behaviors">按顺序注册的异步行为列表</param>
    /// <returns>执行管道后得到的返回值</returns>
    public static ValueTask<T> ExecuteAsync<T>(InvocationContext ctx, Func<ValueTask<T>> inner, IReadOnlyList<IInvocationAsyncBehavior> behaviors)
    {
        Func<ValueTask<T>> next = inner;
        for (int i = behaviors.Count - 1; i >= 0; i--)
        {
            var b = behaviors[i];
            var currentNext = next;
            next = () => b.InvokeAsync(ctx, currentNext);
        }
        return next();
    }

}

