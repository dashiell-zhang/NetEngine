using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;

namespace WebApi.Filters
{

    /// <summary>
    /// 全局过滤器
    /// </summary>
    [AttributeUsage(AttributeTargets.Event)]
    public class GlobalFilter : Attribute, IActionFilter
    {

        void IActionFilter.OnActionExecuting(ActionExecutingContext context)
        {

        }


        void IActionFilter.OnActionExecuted(ActionExecutedContext context)
        {
            if (context.HttpContext.Response.StatusCode == 400)
            {
                string errMsg = context.HttpContext.Items["errMsg"]!.ToString()!;

                context.Result = new JsonResult(new { errMsg });
            }
        }


    }
}
