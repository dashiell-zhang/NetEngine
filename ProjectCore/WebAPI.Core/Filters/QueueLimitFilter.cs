using Common;
using DistributedLock;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WebAPI.Core.Libraries;

namespace WebAPI.Core.Filters
{


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



        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            IDisposable? lockHandle = null;

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

                    lockHandle = await distLock.TryLockAsync(key, expiryTime);
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
                            await Task.Delay(200);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<QueueLimitFilter>>();
                logger.LogError(ex, "队列限制模块异常-In");
            }

            await next();

            try
            {
                if (Expiry <= 0)
                {
                    lockHandle?.Dispose();
                }
            }
            catch (Exception ex)
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<QueueLimitFilter>>();
                logger.LogError(ex, "队列限制模块异常-Out");
            }
        }
    }
}
