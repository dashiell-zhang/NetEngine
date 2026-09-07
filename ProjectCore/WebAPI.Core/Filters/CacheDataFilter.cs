using Common;
using DistributedLock;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using WebAPI.Core.Extensions;

namespace WebAPI.Core.Filters;

/// <summary>
/// 缓存过滤器
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class CacheDataFilter : Attribute, IAsyncActionFilter
{

    /// <summary>
    /// 缓存时效有效期，单位 秒
    /// </summary>
    public int TTL { get; set; }


    /// <summary>
    /// 是否使用 Token
    /// </summary>
    public bool IsUseToken { get; set; }


    /// <summary>
    /// 读取或写入请求缓存并在需要时使用分布式锁防止缓存击穿
    /// </summary>
    /// <param name="context">当前操作筛选器上下文</param>
    /// <param name="next">后续操作委托</param>
    /// <returns>筛选器执行任务</returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {

        var distributedCache = context.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
        var cancellationToken = context.HttpContext.RequestAborted;

        string cacheKey = "";
        IDistributedLockHandle? lockHandle = null;

        try
        {
            var request = context.HttpContext.Request;
            var keyData = new
            {
                Action = context.ActionDescriptor.DisplayName,
                request.Method,
                PathBase = request.PathBase.Value,
                Path = request.Path.Value,
                RouteValues = context.RouteData.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value),
                Parameters = context.HttpContext.GetParameters(),
                Authorization = IsUseToken ? request.Headers.Authorization.ToString() : null
            };

            cacheKey = "CacheData_" + CryptoHelper.MD5HashData(JsonHelper.ObjectToJson(keyData));

            var cacheInfo = await distributedCache.GetAsync<CachedResponse>(cacheKey, cancellationToken);

            if (cacheInfo != null)
            {
                context.Result = cacheInfo.ToObjectResult();

                return;
            }
            else
            {
                var distributedLock = context.HttpContext.RequestServices.GetRequiredService<IDistributedLock>();

                while (true)
                {
                    var expiryTime = TimeSpan.FromSeconds(60);

                    lockHandle = await distributedLock.TryLockAsync(cacheKey, expiryTime, cancellationToken: cancellationToken);
                    if (lockHandle != null)
                    {
                        break;
                    }
                    else
                    {
                        await Task.Delay(200, cancellationToken);

                        cacheInfo = await distributedCache.GetAsync<CachedResponse>(cacheKey, cancellationToken);

                        if (cacheInfo != null)
                        {
                            context.Result = cacheInfo.ToObjectResult();

                            return;
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<CacheDataFilter>>();
            logger.LogError(ex, "缓存模块异常-In");
        }

        try
        {
            var actionExecutedContext = await next();

            try
            {
                if (actionExecutedContext.Result is ObjectResult objectResult && objectResult.Value != null)
                {
                    var cachedResponse = new CachedResponse
                    {
                        StatusCode = objectResult.StatusCode ?? (objectResult.Value as ProblemDetails)?.Status ?? context.HttpContext.Response.StatusCode,
                        Value = objectResult.Value
                    };
                    await distributedCache.SetAsync(cacheKey, cachedResponse, TimeSpan.FromSeconds(TTL), cancellationToken: cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<CacheDataFilter>>();
                logger.LogError(ex, "缓存模块异常-Out");
            }
        }
        finally
        {
            if (lockHandle is not null)
            {
                try
                {
                    await lockHandle.DisposeAsync();
                }
                catch (Exception ex)
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<CacheDataFilter>>();
                    logger.LogError(ex, "缓存模块释放分布式锁异常");
                }
            }
        }

    }


    /// <summary>
    /// 保存对象响应的返回值和 HTTP 状态码
    /// </summary>
    private sealed class CachedResponse
    {

        /// <summary>
        /// 原始对象响应的 HTTP 状态码
        /// </summary>
        public int StatusCode { get; set; }


        /// <summary>
        /// 原始返回值 读取缓存后以 JSON 元素表示
        /// </summary>
        public object? Value { get; set; }


        /// <summary>
        /// 恢复缓存响应并保留字符串结果的原有处理方式
        /// </summary>
        public ObjectResult ToObjectResult()
        {

            var value = Value is JsonElement { ValueKind: JsonValueKind.String } element ? element.GetString() : Value;
            return new ObjectResult(value) { StatusCode = StatusCode };

        }

    }

}
