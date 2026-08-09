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
            var parameters = JsonHelper.ObjectToJson(context.HttpContext.GetParameters());

            cacheKey = parameters;

            if (IsUseToken)
            {
                var token = context.HttpContext.Request.Headers.Where(t => t.Key == "Authorization").Select(t => t.Value).FirstOrDefault();
                cacheKey = cacheKey + "_" + token;
            }

            cacheKey = "CacheData_" + CryptoHelper.MD5HashData(cacheKey);

            var cacheInfo = await distributedCache.GetAsync<object>(cacheKey, cancellationToken);

            if (cacheInfo != null)
            {
                if (((JsonElement)cacheInfo).ValueKind == JsonValueKind.String)
                {
                    context.Result = new ObjectResult(cacheInfo.ToString());
                }
                else
                {
                    context.Result = new ObjectResult(cacheInfo);
                }

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

                        cacheInfo = await distributedCache.GetAsync<object>(cacheKey, cancellationToken);

                        if (cacheInfo != null)
                        {
                            if (((JsonElement)cacheInfo).ValueKind == JsonValueKind.String)
                            {
                                context.Result = new ObjectResult(cacheInfo.ToString());
                            }
                            else
                            {
                                context.Result = new ObjectResult(cacheInfo);
                            }

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
                    await distributedCache.SetAsync(cacheKey, objectResult.Value, TimeSpan.FromSeconds(TTL), cancellationToken: cancellationToken);
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
}
