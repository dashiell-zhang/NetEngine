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
    public class QueueLimitFilter : Attribute, IFilterFactory
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



        public bool IsReusable => false;
        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            QueueLimitFilterAction queueLimitFilterAction = new();

            queueLimitFilterAction.IsUseParameter = IsUseParameter;
            queueLimitFilterAction.IsUseToken = IsUseToken;
            queueLimitFilterAction.IsBlock = IsBlock;
            queueLimitFilterAction.Expiry = Expiry;

            return queueLimitFilterAction;
        }
    }


    internal class QueueLimitFilterAction : IActionFilter
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


        private IDisposable? LockHandle { get; set; }




        void IActionFilter.OnActionExecuting(ActionExecutingContext context)
        {
            string key = context.ActionDescriptor.DisplayName!;

            if (IsUseToken)
            {
                var token = context.HttpContext.Request.Headers.Where(t => t.Key == "Authorization").Select(t => t.Value).FirstOrDefault();
                key = key + "_" + token;
            }

            if (IsUseParameter)
            {
                var parameter = JsonHelper.ObjectToJson(context.HttpContext.GetParameters());
                key = key + "_" + parameter;
            }

            key = "QueueLimit_" + CryptoHelper.MD5HashData(key);

            try
            {
                var distLock = context.HttpContext.RequestServices.GetRequiredService<IDistributedLock>();

                while (true)
                {
                    var expiryTime = TimeSpan.FromSeconds(60);

                    if (Expiry > 0)
                    {
                        expiryTime = TimeSpan.FromSeconds(Expiry);
                    }

                    var handle = distLock.TryLock(key, expiryTime);
                    if (handle != null)
                    {
                        LockHandle = handle;
                        break;
                    }
                    else
                    {
                        if (IsBlock)
                        {
                            context.Result = new BadRequestObjectResult(new { errMsg = "请勿频繁操作" });
                            break;
                        }
                        else
                        {
                            Thread.Sleep(200);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<QueueLimitFilter>>();
                logger.LogError(ex, "队列限制模块异常-In");
            }
        }



        void IActionFilter.OnActionExecuted(ActionExecutedContext context)
        {
            try
            {
                if (Expiry <= 0)
                {
                    LockHandle?.Dispose();
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
