using Common;
using Microsoft.AspNetCore.Diagnostics;
using WebAPI.Core.Extensions;

namespace WebAPI.Core.Libraries;

public class GlobalError
{

    public static Task ErrorEvent(HttpContext httpContext)
    {
        var feature = httpContext.Features.Get<IExceptionHandlerFeature>();
        var error = feature?.Error;

        var ret = new
        {
            errMsg = "系统全局内部异常"
        };

        string path = httpContext.GetUrl();

        var parameters = httpContext.GetParameters();

        var parametersStr = JsonHelper.ObjectToJson(parameters);

        if (parametersStr.Length > 102400)
        {
            _ = parametersStr[..102400];
        }

        var authorization = httpContext.Request.Headers.Authorization.ToString();

        var content = new
        {
            path,
            parameters,
            authorization,
            error = new
            {
                error?.Source,
                error?.Message,
                error?.StackTrace,
                InnerSource = error?.InnerException?.Source,
                InnerMessage = error?.InnerException?.Message,
                InnerStackTrace = error?.InnerException?.StackTrace,
            }
        };

        var logger = httpContext.RequestServices.GetRequiredService<ILogger<GlobalError>>();

        logger.LogError("全局异常：" + JsonHelper.ObjectToJson(content));

        httpContext.Response.StatusCode = 400;

        return httpContext.Response.WriteAsJsonAsync(ret);
    }

}
