using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Methods.Json;

namespace WebApi.Filters
{
    public class CacheData : Attribute, IActionFilter
    {

        /// <summary>
        /// 缓存时效有效期，单位 秒
        /// </summary>
        public int TTL { get; set; }



        void IActionFilter.OnActionExecuting(ActionExecutingContext context)
        {
            var key = context.ActionDescriptor.DisplayName+"_"+ context.HttpContext.Request.QueryString;
            key = "CacheData_"+ Methods.Crypto.Md5.GetMd5(key);

            try
            {

                var cacheInfo = Methods.NoSql.Redis.StrGet(key);

                if (!string.IsNullOrEmpty(cacheInfo))
                {
                    var x = JsonHelper.GetValueByKey(cacheInfo, "Value");

                    context.Result = new ObjectResult(x);
                }
            }
            catch
            {
                Console.WriteLine("缓存模块异常");
            }
        }


        void IActionFilter.OnActionExecuted(ActionExecutedContext context)
        {
            try
            {
                var value = Methods.Json.JsonHelper.ObjectToJSON(context.Result);

                var key = context.ActionDescriptor.DisplayName + "_" + context.HttpContext.Request.QueryString;

                key = "CacheData_" + Methods.Crypto.Md5.GetMd5(key);


                Methods.NoSql.Redis.StrSet(key, value,TimeSpan.FromSeconds(TTL));

            }
            catch
            {
                Console.WriteLine("缓存模块异常");
            }

        }
    }
}
