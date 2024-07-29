using Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

namespace WebAPIBasic.Filters
{


    /// <summary>
    /// ETag计算过滤器
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class ETagFilter : Attribute, IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {

        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            var request = context.HttpContext.Request;
            var response = context.HttpContext.Response;

            if (response.StatusCode == 200 && string.Equals(request.Method, "get", StringComparison.OrdinalIgnoreCase))
            {
                if (context.Result is ObjectResult result)
                {
                    if (result.Value != null)
                    {
                        var etag = CryptoHelper.MD5HashData(JsonHelper.ObjectToJson(result.Value));

                        if (request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var incomingEtag) && incomingEtag == etag)
                        {
                            context.Result = new StatusCodeResult(304);
                        }
                        else
                        {
                            response.Headers[HeaderNames.ETag] = etag;
                        }
                    }
                }
            }
        }


    }
}
