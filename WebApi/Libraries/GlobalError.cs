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


        public static Task ErrorEvent(HttpContext context)
        {
            var feature = context.Features.Get<IExceptionHandlerFeature>();
            var error = feature?.Error;

            var ret = new
            {
                errMsg = "系统全局内部异常"
            };


            string path = Http.HttpContext.GetUrl();

            var parameter = Http.HttpContext.GetParameter();

            var parameterStr = JsonHelper.ObjectToJson(parameter);

            if (parameterStr.Length > 102400)
            {
                parameterStr = parameterStr[..102400];
            }

            var authorization = Http.HttpContext.Current().Request.Headers["Authorization"].ToString();

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

            var db = Http.HttpContext.Current().RequestServices.GetRequiredService<DatabaseContext>();
            var snowflakeHelper = Http.HttpContext.Current().RequestServices.GetRequiredService<SnowflakeHelper>();


            db.CreateLog(snowflakeHelper.GetId(), "WebApi", "errorlog", strContent);

            context.Response.StatusCode = 400;

            return context.Response.WriteAsJsonAsync(ret);
        }


    }
}
