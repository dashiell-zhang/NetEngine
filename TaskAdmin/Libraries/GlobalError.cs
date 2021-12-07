using Common.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace TaskAdmin.Libraries
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

            var content = new
            {
                path,
                parameterStr,
                error = new
                {
                    error.Source,
                    error.Message,
                    error.StackTrace
                }
            };

            string strContent = JsonHelper.ObjectToJson(content);

            Common.DBHelper.LogSet("TaskAdmin", "errorlog", strContent);

            context.Response.StatusCode = 400;

            return context.Response.WriteAsJsonAsync(ret);
        }


    }
}
