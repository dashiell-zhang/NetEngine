using Microsoft.AspNetCore.Rewrite;
using Microsoft.Net.Http.Headers;
using System.Linq;

namespace Cms.Libraries
{


    /// <summary>
    /// 重定向 get 请求 url 到小写 url 模式
    /// </summary>
    public class RedirectLowerCaseUrlsRule : IRule
    {
        public void ApplyRule(RewriteContext context)
        {
            var request = context.HttpContext.Request;

            var host = context.HttpContext.Request.Host;

            var path = context.HttpContext.Request.Path;


            if ((path.HasValue && path.Value.Any(char.IsUpper) || host.HasValue && host.Value.Any(char.IsUpper)) && request.Method.ToLower() == "get")
            {

                var newUrl = (request.Scheme + "://" + host.Value + request.PathBase + request.Path).ToLower() + request.QueryString;

                var response = context.HttpContext.Response;

                response.StatusCode = 301;

                response.Headers[HeaderNames.Location] = newUrl;

                context.Result = RuleResult.EndResponse;
            }

        }
    }
}
