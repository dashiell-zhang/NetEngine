namespace SourceGenerator.Runtime;

public static class InvocationPipeline
{
    public static ValueTask<T> ExecuteAsync<T>(InvocationContext ctx, Func<ValueTask<T>> inner, IReadOnlyList<IInvocationBehavior> behaviors)
    {
        Func<ValueTask<T>> next = inner;
        for (int i = behaviors.Count - 1; i >= 0; i--)
        {
            var b = behaviors[i];
            var currentNext = next;
            next = () => b.InvokeAsync<T>(ctx, currentNext);
        }
        return next();
    }
}

