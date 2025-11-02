namespace SourceGenerator.Runtime;

public sealed class CachingBehavior : IInvocationBehavior
{
    public async ValueTask<T> InvokeAsync<T>(InvocationContext ctx, Func<ValueTask<T>> next)
    {
        var cache = ctx.GetFeature<ProxyRuntime.CacheOptions>();
        if (cache is not null)
        {
            var methodForLog = ctx.Method + " traceId=" + ctx.TraceId.ToString();
            var get = await CacheRuntime.TryGetAsync<T>(ctx.ServiceProvider, cache, ctx.Logger, ctx.Log, methodForLog);
            if (get.hit) return get.value;
        }

        var result = await next();

        if (cache is not null)
        {
            var methodForLog = ctx.Method + " traceId=" + ctx.TraceId.ToString();
            await CacheRuntime.SetAsync(ctx.ServiceProvider, cache, ctx.Logger, ctx.Log, methodForLog, result);
        }
        return result;
    }
}
