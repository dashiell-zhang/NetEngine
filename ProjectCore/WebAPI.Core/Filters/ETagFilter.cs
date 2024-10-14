using Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace WebAPI.Core.Filters
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
                        var etag = new StringValues($"\"{CryptoHelper.SHA256HashData(JsonHelper.ObjectToJson(result.Value), "base64")}\"");

                        if (request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var incomingEtag) && incomingEtag == etag)
                        {
                            context.Result = new StatusCodeResult(304);
                        }
                        else
                        {
                            response.Headers.ETag = etag;
                        }
                    }
                }
            }
        }

    }
}
