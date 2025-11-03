namespace SourceGenerator.Runtime
{
    public interface IInvocationAsyncBehavior
    {
        ValueTask<T> InvokeAsync<T>(InvocationContext ctx, Func<ValueTask<T>> next);
    }
}
