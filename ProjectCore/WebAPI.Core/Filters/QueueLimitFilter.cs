using Common;
using DistributedLock;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WebAPI.Core.Extensions;

namespace WebAPI.Core.Filters;


/// <summary>
/// 队列过滤器
/// </summary>
/// 
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class QueueLimitFilter : Attribute, IAsyncActionFilter
{


    /// <summary>
    /// 是否使用 参数
    /// </summary>
    public bool IsUseParameter { get; set; }


    /// <summary>
    /// 是否使用 Token
    /// </summary>
    public bool IsUseToken { get; set; }


    /// <summary>
    /// 是否阻断重复请求
    /// </summary>
    public bool IsBlock { get; set; }



    /// <summary>
    /// 失效时长（单位秒）
    /// </summary>
    public int Expiry { get; set; }

    /// <summary>
    /// 获取请求级分布式锁并按照配置执行排队或阻断
    /// </summary>
    /// <param name="context">当前操作筛选器上下文</param>
    /// <param name="next">后续操作委托</param>
    /// <returns>筛选器执行任务</returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        IDistributedLockHandle? lockHandle = null;
        var cancellationToken = context.HttpContext.RequestAborted;

        try
        {
            string key = context.ActionDescriptor.DisplayName!;

            if (IsUseToken)
            {
                var token = context.HttpContext.Request.Headers.Where(t => t.Key == "Authorization").Select(t => t.Value).FirstOrDefault();
                key = key + "_" + token;
            }

            if (IsUseParameter)
            {
                var parameters = JsonHelper.ObjectToJson(context.HttpContext.GetParameters());
                key = key + "_" + parameters;
            }

            key = "QueueLimit_" + CryptoHelper.MD5HashData(key);

            var distLock = context.HttpContext.RequestServices.GetRequiredService<IDistributedLock>();

            while (true)
            {
                var expiryTime = TimeSpan.FromSeconds(60);

                if (Expiry > 0)
                {
                    expiryTime = TimeSpan.FromSeconds(Expiry);
                }

                lockHandle = await distLock.TryLockAsync(key, expiryTime, cancellationToken: cancellationToken);
                if (lockHandle != null)
                {
                    break;
                }
                else
                {
                    if (IsBlock)
                    {
                        context.Result = new BadRequestObjectResult(new { errMsg = "请勿频繁操作" });
                        return;
                    }
                    else
                    {
                        await Task.Delay(200, cancellationToken);
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
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<QueueLimitFilter>>();
            logger.LogError(ex, "队列限制模块异常-In");
        }

        try
        {
            await next();
        }
        finally
        {
            if (Expiry <= 0 && lockHandle is not null)
            {
                try
                {
                    await lockHandle.DisposeAsync();
                }
                catch (Exception ex)
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<QueueLimitFilter>>();
                    logger.LogError(ex, "队列限制模块释放分布式锁异常");
                }
            }
        }
    }
}
