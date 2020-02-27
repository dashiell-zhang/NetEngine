using Common.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
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
                errMsg = error.Message
            };


            string path = Libraries.Http.HttpContext.GetUrl();

            var parameter = Libraries.Http.HttpContext.GetParameter();

            var content = new
            {
                path = path,
                parameter = parameter,
                error = error
            };

            string strContent = JsonHelper.ObjectToJSON(content);

            Common.UseDB.Log.Set("Cms", "errorlog", strContent);

            context.Response.StatusCode = 400;

            return context.Response.WriteAsync(JsonHelper.ObjectToJSON(ret));
        }
    }
}
