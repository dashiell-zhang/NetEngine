using Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace AdminApi.Filters
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
        public bool UseToken { get; set; }


        void IActionFilter.OnActionExecuting(ActionExecutingContext context)
        {
            string key = "";

            if (UseToken)
            {
                var token = context.HttpContext.Request.Headers.Where(t => t.Key == "Authorization").Select(t => t.Value).FirstOrDefault();

                key = context.ActionDescriptor.DisplayName + "_" + context.HttpContext.Request.QueryString + "_" + token;
            }
            else
            {
                key = context.ActionDescriptor.DisplayName + "_" + context.HttpContext.Request.QueryString;
            }

            key = "CacheData_" + CryptoHelper.GetMD5(key);

            try
            {
                var distributedCache = context.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
                var cacheInfo = distributedCache.GetObject<object>(key);

                if (cacheInfo != null)
                {
                    context.Result = new ObjectResult(cacheInfo);
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

                    if (UseToken)
                    {
                        var token = context.HttpContext.Request.Headers.Where(t => t.Key == "Authorization").Select(t => t.Value).FirstOrDefault();

                        key = context.ActionDescriptor.DisplayName + "_" + context.HttpContext.Request.QueryString + "_" + token;
                    }
                    else
                    {
                        key = context.ActionDescriptor.DisplayName + "_" + context.HttpContext.Request.QueryString;
                    }

                    key = "CacheData_" + CryptoHelper.GetMD5(key);

                    if (objectResult.Value != null)
                    {
                        var distributedCache = context.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
                        distributedCache.SetObject(key, objectResult.Value, TimeSpan.FromSeconds(TTL));
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
