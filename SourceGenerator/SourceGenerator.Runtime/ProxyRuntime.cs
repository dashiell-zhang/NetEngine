using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SourceGenerator.Runtime;

public static class ProxyRuntime
{

    public static T Execute<T>(InvocationContext ctx, Func<ValueTask<T>> inner)
    {
        var behaviors = ctx.Behaviors ?? new IInvocationBehavior[] { new LoggingBehavior() };
        return InvocationPipeline.ExecuteAsync<T>(ctx, inner, behaviors).GetAwaiter().GetResult();
    }

    public static ValueTask<T> ExecuteAsync<T>(InvocationContext ctx, Func<ValueTask<T>> inner)
    {
        var behaviors = ctx.Behaviors ?? new IInvocationBehavior[] { new LoggingBehavior() };
        return InvocationPipeline.ExecuteAsync<T>(ctx, inner, behaviors);
    }

    public static Task ExecuteTask(InvocationContext ctx, Func<Task> inner)
    {
        var behaviors = ctx.Behaviors ?? new IInvocationBehavior[] { new LoggingBehavior() };
        return InvocationPipeline
            .ExecuteAsync<Unit>(ctx, () => new ValueTask<Unit>(inner().ContinueWith(_ => Unit.Value)), behaviors)
            .AsTask()
            .ContinueWith(_ => { });
    }
}
