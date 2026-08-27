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
    /// 缓存结果序列化配置 包含公开字段并保留循环引用元数据
    /// </summary>
    private static readonly JsonSerializerOptions CacheJsonOptions = new(JsonUtil.JsonOpts)
    {
        IncludeFields = true
    };


    /// <summary>
    /// 保存缓存结果的实际运行时类型和对应 JSON 内容
    /// </summary>
    private sealed class CacheEntry
    {

        /// <summary>
        /// 返回结果的程序集限定运行时类型名称
        /// </summary>
        public string? RuntimeType { get; init; }


        /// <summary>
        /// 按实际运行时类型序列化后的返回结果
        /// </summary>
        public JsonElement Value { get; init; }

    }


    /// <summary>
    /// 缓存回源保护锁的默认租约时长
    /// </summary>
    private static readonly TimeSpan CacheLockExpiry = TimeSpan.FromSeconds(60);

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

        if (cache.TtlSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(Options.CacheableOptions.TtlSeconds), cache.TtlSeconds, "TtlSeconds 必须大于 0");

        if (!ctx.IsArgumentsKeyComplete || ctx.ArgumentsKey is null)
        {
            if (ctx.Logger?.IsEnabled(LogLevel.Warning) == true)
                ctx.Logger.LogWarning("Cache bypassed because arguments key is incomplete method={Method} traceId={TraceId}", ctx.Method, ctx.TraceId);

            return await next();
        }

        var cacheSvc = ctx.ServiceProvider?.GetService(typeof(IDistributedCache)) as IDistributedCache;
        if (cacheSvc is null)
        {
            if (ctx.Logger?.IsEnabled(LogLevel.Warning) == true)
                ctx.Logger.LogWarning("Cache bypassed because IDistributedCache is unavailable method={Method} traceId={TraceId}", ctx.Method, ctx.TraceId);

            return await next();
        }

        var keyHash = InvocationKey.ComposeHash(ctx, includeArguments: true);
        var cacheKey = ComposeCacheKey(keyHash);

        ctx.CancellationToken.ThrowIfCancellationRequested();
        var get = await TryGetAsync<T>(cacheSvc, cacheKey, ctx.Logger, ctx.Method, ctx.TraceId, ctx.CancellationToken);
        if (get.hit) return get.value;

        var lockSvc = ctx.ServiceProvider?.GetService(typeof(IDistributedLock)) as IDistributedLock;
        if (lockSvc is null)
        {
            return await ExecuteAndSetAsync(cacheSvc, next, cacheKey, cache, ctx.Logger, ctx.Method, ctx.TraceId, ctx.CancellationToken);
        }

        var lockKey = ComposeLockKey(keyHash);
        IDistributedLockHandle lockHandle;

        ctx.CancellationToken.ThrowIfCancellationRequested();

        try
        {
            lockHandle = await lockSvc.LockAsync(lockKey, CacheLockExpiry, cancellationToken: ctx.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (ctx.Logger?.IsEnabled(LogLevel.Warning) == true)
                ctx.Logger.LogWarning("Cache stampede lock error method={Method} traceId={TraceId}: {ErrorMessage}", ctx.Method, ctx.TraceId, ex.Message);

            return await ExecuteAndSetAsync(cacheSvc, next, cacheKey, cache, ctx.Logger, ctx.Method, ctx.TraceId, ctx.CancellationToken);
        }

        var leaseRenewer = new DistributedLockLeaseRenewer(lockSvc, lockHandle, CacheLockExpiry, lockKey, ctx.Method, ctx.TraceId, ctx.Logger);

        try
        {
            if (ctx.Logger?.IsEnabled(LogLevel.Information) == true)
                ctx.Logger.LogInformation("Cache stampede lock acquired method={Method} traceId={TraceId}", ctx.Method, ctx.TraceId);

            ctx.CancellationToken.ThrowIfCancellationRequested();
            get = await TryGetAsync<T>(cacheSvc, cacheKey, ctx.Logger, ctx.Method, ctx.TraceId, ctx.CancellationToken);
            if (get.hit) return get.value;

            return await ExecuteAndSetAsync(cacheSvc, next, cacheKey, cache, ctx.Logger, ctx.Method, ctx.TraceId, ctx.CancellationToken);
        }
        finally
        {
            await leaseRenewer.StopAsync();

            try
            {
                await lockHandle.DisposeAsync();

                if (ctx.Logger?.IsEnabled(LogLevel.Information) == true)
                    ctx.Logger.LogInformation("Cache stampede lock released method={Method} traceId={TraceId}", ctx.Method, ctx.TraceId);
            }
            catch (Exception ex)
            {
                if (ctx.Logger?.IsEnabled(LogLevel.Error) == true)
                    ctx.Logger.LogError(ex, "Cache stampede lock release error method={Method} traceId={TraceId}", ctx.Method, ctx.TraceId);
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
    /// <typeparam name="T">调用返回值类型</typeparam>
    /// <param name="cacheSvc">分布式缓存服务</param>
    /// <param name="next">后续行为或目标方法</param>
    /// <param name="cacheKey">结果缓存键</param>
    /// <param name="cache">缓存行为配置</param>
    /// <param name="logger">调用日志记录器</param>
    /// <param name="method">调用方法名称</param>
    /// <param name="traceId">调用跟踪标识</param>
    /// <param name="cancellationToken">调用取消令牌</param>
    /// <returns>业务方法返回结果</returns>
    private static async ValueTask<T> ExecuteAndSetAsync<T>(IDistributedCache cacheSvc, Func<ValueTask<T>> next, string cacheKey, Options.CacheableOptions cache, ILogger? logger, string method, Guid traceId, CancellationToken cancellationToken)
    {

        cancellationToken.ThrowIfCancellationRequested();
        var result = await next();
        await SetAsync(cacheSvc, cacheKey, cache, logger, method, traceId, result, cancellationToken);
        return result;

    }


    /// <summary>
    /// 尝试从分布式缓存中读取结果 返回是否命中及对应值
    /// </summary>
    private static async Task<(bool hit, T value)> TryGetAsync<T>(IDistributedCache cacheSvc, string cacheKey, ILogger? logger, string method, Guid traceId, CancellationToken cancellationToken)
    {

        try
        {
            var json = await cacheSvc.GetStringAsync(cacheKey, cancellationToken);
            if (json is null) return (false, default!);

            var entry = JsonSerializer.Deserialize<CacheEntry>(json, CacheJsonOptions)
                ?? throw new JsonException("缓存内容缺少结果信封");

            if (entry.RuntimeType is null)
            {
                if (entry.Value.ValueKind != JsonValueKind.Null)
                    throw new JsonException("缓存内容缺少运行时类型");

                if (default(T) is not null)
                    throw new JsonException($"缓存空结果与声明类型 {typeof(T).FullName} 不兼容");

                if (logger?.IsEnabled(LogLevel.Information) == true)
                    logger.LogInformation("Cache hit method={Method} traceId={TraceId}", method, traceId);

                return (true, default!);
            }

            var runtimeType = Type.GetType(entry.RuntimeType, throwOnError: false);
            var declaredType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

            if (runtimeType is null || !declaredType.IsAssignableFrom(runtimeType))
                throw new JsonException($"缓存运行时类型 {entry.RuntimeType} 与声明类型 {typeof(T).FullName} 不兼容");

            var result = entry.Value.Deserialize(runtimeType, CacheJsonOptions);

            if (result is not T typedResult)
            {
                if (result is null && default(T) is null)
                {
                    if (logger?.IsEnabled(LogLevel.Information) == true)
                        logger.LogInformation("Cache hit method={Method} traceId={TraceId}", method, traceId);

                    return (true, default!);
                }

                throw new JsonException($"缓存结果无法转换为声明类型 {typeof(T).FullName}");
            }

            if (logger?.IsEnabled(LogLevel.Information) == true)
                logger.LogInformation("Cache hit method={Method} traceId={TraceId}", method, traceId);

            return (true, typedResult);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (logger?.IsEnabled(LogLevel.Information) == true)
                logger.LogInformation("Cache read error method={Method} traceId={TraceId}: {ErrorMessage}", method, traceId, ex.Message);
            
            return (false, default!);
        }
    }


    /// <summary>
    /// 在业务成功后尽力将返回结果写入分布式缓存
    /// </summary>
    /// <typeparam name="T">调用返回值类型</typeparam>
    /// <param name="cacheSvc">分布式缓存服务</param>
    /// <param name="cacheKey">结果缓存键</param>
    /// <param name="cache">缓存行为配置</param>
    /// <param name="logger">调用日志记录器</param>
    /// <param name="method">调用方法名称</param>
    /// <param name="traceId">调用跟踪标识</param>
    /// <param name="value">业务方法返回结果</param>
    /// <param name="cancellationToken">调用取消令牌</param>
    private static async Task SetAsync<T>(IDistributedCache cacheSvc, string cacheKey, Options.CacheableOptions cache, ILogger? logger, string method, Guid traceId, T value, CancellationToken cancellationToken)
    {

        if (cancellationToken.IsCancellationRequested)
        {
            if (logger?.IsEnabled(LogLevel.Information) == true)
                logger.LogInformation("Cache write skipped because request was canceled after method completed method={Method} traceId={TraceId}", method, traceId);

            return;
        }

        try
        {
            var runtimeType = value?.GetType();
            var serializationType = runtimeType ?? typeof(T);
            var serializedValue = JsonSerializer.SerializeToElement(value, serializationType, CacheJsonOptions);

            var entry = new CacheEntry
            {
                RuntimeType = runtimeType?.AssemblyQualifiedName,
                Value = serializedValue
            };
            var json = JsonSerializer.Serialize(entry, CacheJsonOptions);
            await cacheSvc.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cache.TtlSeconds)
            }, cancellationToken);

            if (logger?.IsEnabled(LogLevel.Information) == true)
                logger.LogInformation("Cache set method={Method} traceId={TraceId} ttl={TtlSeconds}", method, traceId, cache.TtlSeconds);
        }
        catch (OperationCanceledException)
        {
            if (logger?.IsEnabled(LogLevel.Information) == true)
                logger.LogInformation("Cache write canceled after method completed method={Method} traceId={TraceId}", method, traceId);
        }
        catch (Exception ex)
        {
            if (logger?.IsEnabled(LogLevel.Information) == true)
                logger.LogInformation("Cache write error method={Method} traceId={TraceId}: {ErrorMessage}", method, traceId, ex.Message);
        }
    }
}
