using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SourceGenerator.Runtime;

public static class ProxyRuntime
{
    public sealed class CacheOptions
    {
        public required string Seed { get; init; }
        public int TtlSeconds { get; init; }
    }

    public static T Execute<T>(InvocationContext ctx, Func<ValueTask<T>> inner)
    {
        var hasCache = ctx.GetFeature<ProxyRuntime.CacheOptions>() is not null;
        var behaviors = ctx.Behaviors ?? (!hasCache
            ? new IInvocationBehavior[] { new LoggingBehavior() }
            : new IInvocationBehavior[] { new LoggingBehavior(), new CachingBehavior() });
        return InvocationPipeline.ExecuteAsync<T>(ctx, inner, behaviors).GetAwaiter().GetResult();
    }

    public static ValueTask<T> ExecuteAsync<T>(InvocationContext ctx, Func<ValueTask<T>> inner)
    {
        var hasCache = ctx.GetFeature<ProxyRuntime.CacheOptions>() is not null;
        var behaviors = ctx.Behaviors ?? (!hasCache
            ? new IInvocationBehavior[] { new LoggingBehavior() }
            : new IInvocationBehavior[] { new LoggingBehavior(), new CachingBehavior() });
        return InvocationPipeline.ExecuteAsync<T>(ctx, inner, behaviors);
    }

    public static Task ExecuteTask(InvocationContext ctx, Func<Task> inner)
    {
        var hasCache = ctx.GetFeature<ProxyRuntime.CacheOptions>() is not null;
        var behaviors = ctx.Behaviors ?? (!hasCache
            ? new IInvocationBehavior[] { new LoggingBehavior() }
            : new IInvocationBehavior[] { new LoggingBehavior(), new CachingBehavior() });
        return InvocationPipeline
            .ExecuteAsync<Unit>(ctx, () => new ValueTask<Unit>(inner().ContinueWith(_ => Unit.Value)), behaviors)
            .AsTask()
            .ContinueWith(_ => { });
    }
}
