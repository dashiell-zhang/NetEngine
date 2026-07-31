using DistributedLock;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
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

        if (!ctx.IsArgumentsKeyComplete || ctx.ArgumentsKey is null)
        {
            if (ctx.Log) ctx.Logger?.LogWarning($"Cache bypassed because arguments key is incomplete {methodForLog}");
            return await next();
        }

        var keyHash = InvocationKey.ComposeHash(ctx, includeArguments: true);
        var cacheKey = ComposeCacheKey(keyHash);

        var get = await TryGetAsync<T>(ctx, cacheKey, ctx.Logger, ctx.Log, methodForLog);
        if (get.hit) return get.value;

        var lockSvc = ctx.ServiceProvider?.GetService(typeof(IDistributedLock)) as IDistributedLock;
        if (lockSvc is null)
        {
            return await ExecuteAndSetAsync(ctx, next, cacheKey, cache, ctx.Logger, ctx.Log, methodForLog);
        }

        var lockKey = ComposeLockKey(keyHash);
        IDisposable lockHandle;

        try
        {
            lockHandle = await lockSvc.LockAsync(lockKey, TimeSpan.FromSeconds(CacheLockExpirySeconds));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (ctx.Log) ctx.Logger?.LogWarning($"Cache stampede lock error {methodForLog}: {ex.Message}");
            return await ExecuteAndSetAsync(ctx, next, cacheKey, cache, ctx.Logger, ctx.Log, methodForLog);
        }

        try
        {
            if (ctx.Log) ctx.Logger?.LogInformation($"Cache stampede lock acquired {methodForLog}");

            get = await TryGetAsync<T>(ctx, cacheKey, ctx.Logger, ctx.Log, methodForLog);
            if (get.hit) return get.value;

            return await ExecuteAndSetAsync(ctx, next, cacheKey, cache, ctx.Logger, ctx.Log, methodForLog);
        }
        finally
        {
            try
            {
                lockHandle.Dispose();
                if (ctx.Log) ctx.Logger?.LogInformation($"Cache stampede lock released {methodForLog}");
            }
            catch (Exception ex)
            {
                if (ctx.Log) ctx.Logger?.LogInformation($"Cache stampede lock release error {methodForLog}: {ex.Message}");
            }
        }

    }


    /// <summary>
    /// 生成当前调用摘要对应的缓存键
    /// </summary>
    private static string ComposeCacheKey(string keyHash)
        => "CacheData_" + keyHash;


    /// <summary>
    /// 生成当前调用摘要对应的防击穿锁键
    /// </summary>
    private static string ComposeLockKey(string keyHash)
        => "CacheDataLock_" + keyHash;


    /// <summary>
    /// 执行业务回源并在成功后尝试写入缓存
    /// </summary>
    private static async ValueTask<T> ExecuteAndSetAsync<T>(InvocationContext ctx, Func<ValueTask<T>> next, string cacheKey, Options.CacheableOptions cache, ILogger? logger, bool log, string method)
    {

        var result = await next();
        await SetAsync(ctx, cacheKey, cache, logger, log, method, result);
        return result;

    }


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
}
