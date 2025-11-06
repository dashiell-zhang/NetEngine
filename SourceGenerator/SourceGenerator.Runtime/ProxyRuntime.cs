using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SourceGenerator.Runtime.Pipeline;

namespace SourceGenerator.Runtime;

public static class ProxyRuntime
{

    public static T Execute<T>(InvocationContext ctx, Func<ValueTask<T>> inner)
    {
        var behaviors = ctx.Behaviors ?? new IInvocationAsyncBehavior[] { new SourceGenerator.Runtime.Pipeline.Behaviors.LoggingBehavior() };
        return InvocationPipeline.ExecuteAsync<T>(ctx, inner, behaviors).GetAwaiter().GetResult();
    }

    public static ValueTask<T> ExecuteAsync<T>(InvocationContext ctx, Func<ValueTask<T>> inner)
    {
        var behaviors = ctx.Behaviors ?? new IInvocationAsyncBehavior[] { new SourceGenerator.Runtime.Pipeline.Behaviors.LoggingBehavior() };
        return InvocationPipeline.ExecuteAsync<T>(ctx, inner, behaviors);
    }

    public static Task<T> ExecuteAsync<T>(InvocationContext ctx, Func<Task<T>> inner)
    {
        var behaviors = ctx.Behaviors ?? new IInvocationAsyncBehavior[] { new SourceGenerator.Runtime.Pipeline.Behaviors.LoggingBehavior() };
        return InvocationPipeline
            .ExecuteAsync<T>(ctx, async () => await inner().ConfigureAwait(false), behaviors)
            .AsTask();
    }

    public static Task ExecuteTask(InvocationContext ctx, Func<Task> inner)
    {
        var behaviors = ctx.Behaviors ?? new IInvocationAsyncBehavior[] { new SourceGenerator.Runtime.Pipeline.Behaviors.LoggingBehavior() };
        return InvocationPipeline
            .ExecuteAsync<object?>(ctx, async () => { await inner().ConfigureAwait(false); return null; }, behaviors)
            .AsTask();
    }

    public static async ValueTask ExecuteTask(InvocationContext ctx, Func<ValueTask> inner)
    {
        var behaviors = ctx.Behaviors ?? new IInvocationAsyncBehavior[] { new SourceGenerator.Runtime.Pipeline.Behaviors.LoggingBehavior() };
        await InvocationPipeline
            .ExecuteAsync<object?>(ctx, async () => { await inner().ConfigureAwait(false); return null; }, behaviors)
            .ConfigureAwait(false);
    }
}
