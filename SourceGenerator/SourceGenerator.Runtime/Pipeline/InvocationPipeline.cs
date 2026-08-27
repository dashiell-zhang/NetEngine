namespace SourceGenerator.Runtime.Pipeline;

/// <summary>
/// 提供构建并执行异步调用行为管道的辅助方法
/// </summary>
public static class InvocationPipeline
{

    /// <summary>
    /// 根据调用上下文中的行为列表组装管道并执行最终结果
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="ctx">调用上下文</param>
    /// <param name="inner">最终实际执行目标方法的委托</param>
    /// <returns>执行管道后得到的返回值</returns>
    public static ValueTask<T> ExecuteAsync<T>(InvocationContext ctx, Func<ValueTask<T>> inner)
    {

        var behaviors = ctx.Behaviors;

        if (behaviors.Count == 0)
            return inner();

        if (behaviors.Count == 1)
            return behaviors[0].InvokeAsync(ctx, inner);

        Func<ValueTask<T>> next = inner;

        for (var i = behaviors.Count - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var currentNext = next;
            next = () => behavior.InvokeAsync(ctx, currentNext);
        }

        return next();

    }

}
