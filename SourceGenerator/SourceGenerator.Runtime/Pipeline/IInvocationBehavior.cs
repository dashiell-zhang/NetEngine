namespace SourceGenerator.Runtime;

public interface IInvocationBehavior
{
    ValueTask<T> InvokeAsync<T>(InvocationContext ctx, Func<ValueTask<T>> next);
}

