namespace SourceGenerator.Runtime;

public interface IInvocationBehavior
{
    void OnBefore(InvocationContext ctx);
    void OnAfter(InvocationContext ctx, object? result);
    void OnException(InvocationContext ctx, Exception ex);
}

