using Common;
using DotNetCore.CAP;
using Medallion.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;

namespace AdminApi.Libraries
{
    public class ControllerCore : ControllerBase
    {


        public readonly DatabaseContext db;
        public readonly ICapPublisher cap;
        public readonly long userId;
        public readonly IDistributedLockProvider distLock;
        public readonly IDistributedSemaphoreProvider distSemaphoreLock;
        public readonly IDistributedUpgradeableReaderWriterLockProvider distUpgradeableLock;
        public readonly SnowflakeHelper snowflakeHelper;


        protected ControllerCore()
        {
            db = Http.HttpContext.Current().RequestServices.GetService<DatabaseContext>();
            cap = Http.HttpContext.Current().RequestServices.GetService<ICapPublisher>();
            distLock = Http.HttpContext.Current().RequestServices.GetService<IDistributedLockProvider>();
            distSemaphoreLock = Http.HttpContext.Current().RequestServices.GetService<IDistributedSemaphoreProvider>();
            distUpgradeableLock = Http.HttpContext.Current().RequestServices.GetService<IDistributedUpgradeableReaderWriterLockProvider>();
            snowflakeHelper = Http.HttpContext.Current().RequestServices.GetService<SnowflakeHelper>();

            var userIdStr = Verify.JWTToken.GetClaims("userId");

            if (userIdStr != null)
            {
                userId = long.Parse(userIdStr);
            }
        }

    }
}
