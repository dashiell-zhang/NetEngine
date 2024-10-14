using Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace WebAPI.Core.Filters
{


    /// <summary>
    /// 缓存过滤器
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class CacheDataFilter : Attribute, IActionFilter
    {

        /// <summary>
        /// 缓存时效有效期，单位 秒
        /// </summary>
        public int TTL { get; set; }



        /// <summary>
        /// 是否使用 Token
        /// </summary>
        public bool IsUseToken { get; set; }


        void IActionFilter.OnActionExecuting(ActionExecutingContext context)
        {
            string key = "";

            if (IsUseToken)
            {
                var token = context.HttpContext.Request.Headers.Where(t => t.Key == "Authorization").Select(t => t.Value).FirstOrDefault();

                key = context.ActionDescriptor.DisplayName + "_" + context.HttpContext.Request.QueryString + "_" + token;
            }
            else
            {
                key = context.ActionDescriptor.DisplayName + "_" + context.HttpContext.Request.QueryString;
            }

            key = "CacheData_" + CryptoHelper.MD5HashData(key);

            try
            {
                var distributedCache = context.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
                var cacheInfo = distributedCache.Get<object>(key);

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
                }
            }
            catch (Exception ex)
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<CacheDataFilter>>();
                logger.LogError(ex, "缓存模块异常-In");
            }
        }


        void IActionFilter.OnActionExecuted(ActionExecutedContext context)
        {
            try
            {
                if (context.Result is ObjectResult objectResult && objectResult.Value != null)
                {
                    string key = "";

                    if (IsUseToken)
                    {
                        var token = context.HttpContext.Request.Headers.Where(t => t.Key == "Authorization").Select(t => t.Value).FirstOrDefault();

                        key = context.ActionDescriptor.DisplayName + "_" + context.HttpContext.Request.QueryString + "_" + token;
                    }
                    else
                    {
                        key = context.ActionDescriptor.DisplayName + "_" + context.HttpContext.Request.QueryString;
                    }

                    key = "CacheData_" + CryptoHelper.MD5HashData(key);

                    if (objectResult.Value != null)
                    {
                        var distributedCache = context.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
                        distributedCache.SetAsync(key, objectResult.Value, TimeSpan.FromSeconds(TTL));
                    }

                }
            }
            catch (Exception ex)
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<CacheDataFilter>>();
                logger.LogError(ex, "缓存模块异常-Out");
            }

        }
    }
}
