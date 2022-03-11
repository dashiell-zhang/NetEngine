using Common;
using DotNetCore.CAP;
using Medallion.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;

namespace WebApi.Libraries
{
    public class ControllerCore : ControllerBase
    {


        public readonly DatabaseContext db;
        public readonly ICapPublisher cap;
        public readonly long userId;
        public readonly IDistributedLockProvider distLock;
        public readonly IDistributedSemaphoreProvider distSemaphoreLock;
        public readonly IDistributedUpgradeableReaderWriterLockProvider distReaderWriterLock;
        public readonly SnowflakeHelper snowflakeHelper;



        protected ControllerCore()
        {
            db = Http.HttpContext.Current().RequestServices.GetRequiredService<DatabaseContext>();
            cap = Http.HttpContext.Current().RequestServices.GetRequiredService<ICapPublisher>();
            distLock = Http.HttpContext.Current().RequestServices.GetRequiredService<IDistributedLockProvider>();
            distSemaphoreLock = Http.HttpContext.Current().RequestServices.GetRequiredService<IDistributedSemaphoreProvider>();
            distReaderWriterLock = Http.HttpContext.Current().RequestServices.GetRequiredService<IDistributedUpgradeableReaderWriterLockProvider>();
            snowflakeHelper = Http.HttpContext.Current().RequestServices.GetRequiredService<SnowflakeHelper>();


            var userIdStr = Verify.JWTToken.GetClaims("userId");

            if (userIdStr != null)
            {
                userId = long.Parse(userIdStr);
            }
        }

    }
}
