using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SourceGenerator.Runtime.Pipeline.Behaviors;

/// <summary>
/// 为可缓存的调用提供基于分布式缓存的结果缓存行为
/// </summary>
public sealed class CacheableBehavior : IInvocationAsyncBehavior
{

    /// <summary>
    /// 尝试从缓存中读取结果 未命中时调用下游并将结果写入缓存
    /// </summary>
    public async ValueTask<T> InvokeAsync<T>(InvocationContext ctx, Func<ValueTask<T>> next)
    {
        var cache = ctx.GetFeature<Options.CacheableOptions>();

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


    /// <summary>
    /// 组合当前调用的缓存种子字符串 用于生成缓存键
    /// </summary>
    private static string ComposeSeed(InvocationContext ctx)
        => (ctx.Method ?? string.Empty) + (ctx.Args is null ? string.Empty : JsonUtil.ToJson(ctx.Args));


    /// <summary>
    /// 尝试从分布式缓存中读取结果 返回是否命中及对应值
    /// </summary>
    private static async Task<(bool hit, T value)> TryGetAsync<T>(InvocationContext ctx, Options.CacheableOptions cache, ILogger? logger, bool log, string method)
    {
        var cacheSvc = ctx.ServiceProvider?.GetService(typeof(IDistributedCache)) as IDistributedCache;
        
        if (cacheSvc is null) return (false, default!);
        
        try
        {
            var key = "CacheData_" + Sha256Hex(ComposeSeed(ctx));
            var json = await cacheSvc.GetStringAsync(key);
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


    /// <summary>
    /// 将方法返回结果写入分布式缓存
    /// </summary>
    private static async Task SetAsync<T>(InvocationContext ctx, Options.CacheableOptions cache, ILogger? logger, bool log, string method, T value)
    {
        var cacheSvc = ctx.ServiceProvider?.GetService(typeof(IDistributedCache)) as IDistributedCache;
        
        if (cacheSvc is null) return;
        
        try
        {
            var key = "CacheData_" + Sha256Hex(ComposeSeed(ctx));
            var json = JsonSerializer.Serialize(value, JsonUtil.JsonOpts);
            await cacheSvc.SetStringAsync(key, json, new DistributedCacheEntryOptions
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


    /// <summary>
    /// 计算字符串的 SHA-256 哈希并返回十六进制表示
    /// </summary>
    private static string Sha256Hex(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        var hash = SHA256.HashData(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

}
