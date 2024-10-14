using Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WebAPI.Core.Filters
{

    /// <summary>
    /// 异常过滤器
    /// </summary>
    public class ExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            if (context.Exception is CustomException customException)
            {
                context.HttpContext.Response.StatusCode = 400;
                context.Result = new JsonResult(new { errMsg = customException.Message });
            }
        }
    }
}
