using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Json;

namespace WebApi.Filters
{
    public class GlobalFilter : Attribute, IActionFilter
    {

        void IActionFilter.OnActionExecuting(ActionExecutingContext context)
        {

        }


        void IActionFilter.OnActionExecuted(ActionExecutedContext context)
        {
            if (context.HttpContext.Response.StatusCode == 400)
            {
                string errMsg = context.HttpContext.Items["errMsg"].ToString();

                context.Result = new JsonResult(new { errMsg = errMsg });
            }
        }
    }
}
