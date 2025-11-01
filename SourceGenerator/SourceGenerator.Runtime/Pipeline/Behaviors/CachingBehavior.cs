namespace SourceGenerator.Runtime;

public sealed class CachingBehavior : IInvocationBehavior
{
    public async ValueTask<T> InvokeAsync<T>(InvocationContext ctx, Func<ValueTask<T>> next)
    {
        var cache = ctx.Cache;
        if (cache is not null)
        {
            var get = await CacheRuntime.TryGetAsync<T>(ctx.ServiceProvider, cache, ctx.Logger, ctx.Log, ctx.Method);
            if (get.hit) return get.value;
        }

        var result = await next();

        if (cache is not null)
        {
            await CacheRuntime.SetAsync(ctx.ServiceProvider, cache, ctx.Logger, ctx.Log, ctx.Method, result);
        }
        return result;
    }
}

