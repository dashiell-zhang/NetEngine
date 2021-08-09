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
                errMsg = "Global internal exception of the system"
            };


            string path = Http.HttpContext.GetUrl();

            var parameter = Http.HttpContext.GetParameter();

            var parameterStr = JsonHelper.ObjectToJSON(parameter);

            if (parameterStr.Length > 102400)
            {
                parameterStr = parameterStr.Substring(0, 102400);
            }

            var content = new
            {
                path = path,
                parameter = parameterStr,
                error = error
            };

            string strContent = JsonHelper.ObjectToJSON(content);

            Common.DBHelper.LogSet("TaskAdmin", "errorlog", strContent);

            context.Response.StatusCode = 400;

            return context.Response.WriteAsJsonAsync(ret);
        }


    }
}
