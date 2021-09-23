using DotNetCore.CAP;
using Medallion.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using System;

namespace WebApi.Libraries
{
    public class ControllerCore : ControllerBase
    {


        public readonly dbContext db;
        public readonly ICapPublisher cap;
        public readonly Guid userId;
        public readonly IDistributedLockProvider distLock;
        public readonly IDistributedSemaphoreProvider distSemaphoreLock;
        public readonly IDistributedUpgradeableReaderWriterLockProvider distUpgradeableLock;



        protected ControllerCore()
        {
            db = Http.HttpContext.Current().RequestServices.GetService<dbContext>();
            cap = Http.HttpContext.Current().RequestServices.GetService<ICapPublisher>();
            distLock = Http.HttpContext.Current().RequestServices.GetService<IDistributedLockProvider>();
            distSemaphoreLock = Http.HttpContext.Current().RequestServices.GetService<IDistributedSemaphoreProvider>();
            distUpgradeableLock = Http.HttpContext.Current().RequestServices.GetService<IDistributedUpgradeableReaderWriterLockProvider>();


            var userIdStr = Verify.JWTToken.GetClaims("userId");

            if (userIdStr != null)
            {
                userId = Guid.Parse(userIdStr);
            }
        }

    }
}
