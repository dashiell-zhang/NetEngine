using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace SourceGenerator.Runtime;

public sealed class CacheableBehavior : IInvocationAsyncBehavior
{
    public async ValueTask<T> InvokeAsync<T>(InvocationContext ctx, Func<ValueTask<T>> next)
    {
        var cache = ctx.GetFeature<SourceGenerator.Runtime.Options.CacheableOptions>();
        if (cache is not null && ctx.HasReturnValue)
        {
            var methodForLog = ctx.Method + " traceId=" + ctx.TraceId.ToString();
            var get = await TryGetAsync<T>(ctx, cache, ctx.Logger, ctx.Log, methodForLog);
            if (get.hit) return get.value;
        }

        var result = await next();

        if (cache is not null && ctx.HasReturnValue)
        {
            var methodForLog = ctx.Method + " traceId=" + ctx.TraceId.ToString();
            await SetAsync(ctx, cache, ctx.Logger, ctx.Log, methodForLog, result);
        }
        return result;
    }

    private static string ComposeSeed(InvocationContext ctx)
        => (ctx.Method ?? string.Empty) + (ctx.ArgsJson ?? string.Empty);

    private static async Task<(bool hit, T value)> TryGetAsync<T>(InvocationContext ctx, SourceGenerator.Runtime.Options.CacheableOptions cache, ILogger? logger, bool log, string method)
    {
        var cacheSvc = ctx.ServiceProvider?.GetService(typeof(IDistributedCache)) as IDistributedCache;
        if (cacheSvc is null) return (false, default!);
        try
        {
            var key = "CacheData_" + Md5Hex(ComposeSeed(ctx));
            var json = await DistributedCacheExtensions.GetStringAsync(cacheSvc, key);
            if (json is null) return (false, default!);
            if (log) logger?.LogInformation($"Cache hit {method}");
            return (true, JsonSerializer.Deserialize<T>(json, JsonUtil.JsonOpts)!);
        }
        catch (Exception ex)
        {
            if (log) logger?.LogInformation($"Cache read error {method}: {ex.Message}");
            return (false, default!);
        }
    }

    private static async Task SetAsync<T>(InvocationContext ctx, SourceGenerator.Runtime.Options.CacheableOptions cache, ILogger? logger, bool log, string method, T value)
    {
        var cacheSvc = ctx.ServiceProvider?.GetService(typeof(IDistributedCache)) as IDistributedCache;
        if (cacheSvc is null) return;
        try
        {
            var key = "CacheData_" + Md5Hex(ComposeSeed(ctx));
            var json = JsonSerializer.Serialize(value, JsonUtil.JsonOpts);
            await DistributedCacheExtensions.SetStringAsync(cacheSvc, key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cache.TtlSeconds)
            });
            if (log) logger?.LogInformation($"Cache set {method} ttl={cache.TtlSeconds}");
        }
        catch (Exception ex)
        {
            if (log) logger?.LogInformation($"Cache write error {method}: {ex.Message}");
        }
    }

    private static string Md5Hex(string s)
    {
        using var md5 = MD5.Create();
        var bytes = Encoding.UTF8.GetBytes(s);
        var hash = md5.ComputeHash(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
