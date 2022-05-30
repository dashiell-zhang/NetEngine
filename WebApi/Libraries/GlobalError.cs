using Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using Repository.Extensions;
using System.Threading.Tasks;

namespace WebApi.Libraries
{


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

            var parameter = httpContext.GetParameter();

            var parameterStr = JsonHelper.ObjectToJson(parameter);

            if (parameterStr.Length > 102400)
            {
                _ = parameterStr[..102400];
            }

            var authorization = httpContext.Request.Headers["Authorization"].ToString();

            var content = new
            {
                path,
                parameter,
                authorization,
                error = new
                {
                    error?.Source,
                    error?.Message,
                    error?.StackTrace
                }
            };

            string strContent = JsonHelper.ObjectToJson(content);

            var db = httpContext.RequestServices.GetRequiredService<DatabaseContext>();
            var snowflakeHelper = httpContext.RequestServices.GetRequiredService<SnowflakeHelper>();


            db.CreateLog(snowflakeHelper.GetId(), "WebApi", "errorlog", strContent);

            httpContext.Response.StatusCode = 400;

            return httpContext.Response.WriteAsJsonAsync(ret);
        }


    }
}
