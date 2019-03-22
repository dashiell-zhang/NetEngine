using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Cms.Filters
{

    /// <summary>
    /// 域名识别过滤器
    /// </summary>
    public class HostSign : Attribute, IActionFilter
    {
        void IActionFilter.OnActionExecuted(ActionExecutedContext context)
        {

        }

        void IActionFilter.OnActionExecuting(ActionExecutingContext context)
        {

            string host = context.HttpContext.Request.Host.Host;

        }
    }
}
