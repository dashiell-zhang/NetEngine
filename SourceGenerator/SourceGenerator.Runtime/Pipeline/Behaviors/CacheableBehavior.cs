using DistributedLock;
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
    /// 缓存回源保护锁的默认失效时长 秒
    /// </summary>
    private const int CacheLockExpirySeconds = 60;

    /// <summary>
    /// 尝试从缓存中读取结果 未命中时调用下游并将结果写入缓存
    /// </summary>
    public async ValueTask<T> InvokeAsync<T>(InvocationContext ctx, Func<ValueTask<T>> next)
    {
        var cache = ctx.GetFeature<Options.CacheableOptions>();

        if (cache is null || !ctx.HasReturnValue)
        {
            return await next();
        }

        var methodForLog = ctx.Method + " traceId=" + ctx.TraceId.ToString();
        var cacheKey = ComposeCacheKey(ctx);

        var get = await TryGetAsync<T>(ctx, cacheKey, ctx.Logger, ctx.Log, methodForLog);
        if (get.hit) return get.value;

        var lockSvc = ctx.ServiceProvider?.GetService(typeof(IDistributedLock)) as IDistributedLock;
        if (lockSvc is null)
        {
            var resultWithoutLock = await next();
            await SetAsync(ctx, cacheKey, cache, ctx.Logger, ctx.Log, methodForLog, resultWithoutLock);
            return resultWithoutLock;
        }

        IDisposable? lockHandle = null;
        try
        {
            var lockKey = ComposeLockKey(cacheKey);
            lockHandle = await lockSvc.LockAsync(lockKey, TimeSpan.FromSeconds(CacheLockExpirySeconds));
            if (ctx.Log) ctx.Logger?.LogInformation($"Cache stampede lock acquired {methodForLog}");

            get = await TryGetAsync<T>(ctx, cacheKey, ctx.Logger, ctx.Log, methodForLog);
            if (get.hit) return get.value;

            var result = await next();
            await SetAsync(ctx, cacheKey, cache, ctx.Logger, ctx.Log, methodForLog, result);
            return result;
        }
        finally
        {
            try
            {
                lockHandle?.Dispose();
                if (lockHandle is not null && ctx.Log) ctx.Logger?.LogInformation($"Cache stampede lock released {methodForLog}");
            }
            catch (Exception ex)
            {
                if (ctx.Log) ctx.Logger?.LogInformation($"Cache stampede lock release error {methodForLog}: {ex.Message}");
            }
        }
    }


    /// <summary>
    /// 组合当前调用的缓存种子字符串 用于生成缓存键
    /// </summary>
    private static string ComposeSeed(InvocationContext ctx)
        => (ctx.Method ?? string.Empty) + (ctx.Args is null ? string.Empty : JsonUtil.ToJson(ctx.Args));


    /// <summary>
    /// 生成当前调用对应的缓存键
    /// </summary>
    private static string ComposeCacheKey(InvocationContext ctx)
        => "CacheData_" + Sha256Hex(ComposeSeed(ctx));


    /// <summary>
    /// 生成当前缓存键对应的防击穿锁键
    /// </summary>
    private static string ComposeLockKey(string cacheKey)
        => "CacheDataLock_" + cacheKey;


    /// <summary>
    /// 尝试从分布式缓存中读取结果 返回是否命中及对应值
    /// </summary>
    private static async Task<(bool hit, T value)> TryGetAsync<T>(InvocationContext ctx, string cacheKey, ILogger? logger, bool log, string method)
    {
        var cacheSvc = ctx.ServiceProvider?.GetService(typeof(IDistributedCache)) as IDistributedCache;
        
        if (cacheSvc is null) return (false, default!);
        
        try
        {
            var json = await cacheSvc.GetStringAsync(cacheKey);
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
    private static async Task SetAsync<T>(InvocationContext ctx, string cacheKey, Options.CacheableOptions cache, ILogger? logger, bool log, string method, T value)
    {
        var cacheSvc = ctx.ServiceProvider?.GetService(typeof(IDistributedCache)) as IDistributedCache;
        
        if (cacheSvc is null) return;
        
        try
        {
            var json = JsonSerializer.Serialize(value, JsonUtil.JsonOpts);
            await cacheSvc.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
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
