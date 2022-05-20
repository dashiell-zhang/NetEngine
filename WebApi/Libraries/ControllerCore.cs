using Common;
using Common.DistributedLock;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;

namespace WebApi.Libraries
{
    public class ControllerCore : ControllerBase
    {


        public readonly DatabaseContext db;
        public readonly long userId;
        public readonly IDistributedLock distLock;
        public readonly SnowflakeHelper snowflakeHelper;



        protected ControllerCore()
        {
            db = Http.HttpContext.Current().RequestServices.GetRequiredService<DatabaseContext>();
            distLock = Http.HttpContext.Current().RequestServices.GetRequiredService<IDistributedLock>();
            snowflakeHelper = Http.HttpContext.Current().RequestServices.GetRequiredService<SnowflakeHelper>();


            var userIdStr = Verify.JWTToken.GetClaims("userId");

            if (userIdStr != null)
            {
                userId = long.Parse(userIdStr);
            }
        }

    }
}
