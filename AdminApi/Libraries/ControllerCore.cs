using Common;
using Common.RedisLock.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;

namespace AdminApi.Libraries
{
    public class ControllerCore : ControllerBase
    {


        public readonly DatabaseContext db;
        public readonly long userId;
        public readonly IDistributedLockProvider distLock;
        public readonly IDistributedSemaphoreProvider distSemaphoreLock;
        public readonly IDistributedReaderWriterLockProvider distReaderWriterLock;
        public readonly SnowflakeHelper snowflakeHelper;


        protected ControllerCore()
        {
            db = Http.HttpContext.Current().RequestServices.GetRequiredService<DatabaseContext>();
            distLock = Http.HttpContext.Current().RequestServices.GetRequiredService<IDistributedLockProvider>();
            distSemaphoreLock = Http.HttpContext.Current().RequestServices.GetRequiredService<IDistributedSemaphoreProvider>();
            distReaderWriterLock = Http.HttpContext.Current().RequestServices.GetRequiredService<IDistributedReaderWriterLockProvider>();
            snowflakeHelper = Http.HttpContext.Current().RequestServices.GetRequiredService<SnowflakeHelper>();

            var userIdStr = Verify.JWTToken.GetClaims("userId");

            if (userIdStr != null)
            {
                userId = long.Parse(userIdStr);
            }
        }

    }
}
