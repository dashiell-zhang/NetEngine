namespace SourceGenerator.Runtime;

public interface IInvocationAsyncBehavior
{
    ValueTask<T> InvokeAsync<T>(InvocationContext ctx, Func<ValueTask<T>> next);
}

// Sync-friendly hook interface for methods that cannot be wrapped via a delegate (e.g., with ref/out parameters)
public interface IInvocationBehavior
{
    void OnBefore(InvocationContext ctx);
    void OnAfter(InvocationContext ctx, object? result);
    void OnException(InvocationContext ctx, Exception ex);
}

