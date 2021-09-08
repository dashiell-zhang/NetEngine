using DotNetCore.CAP;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using System;

namespace AdminApi.Libraries
{
    public class ControllerCore : ControllerBase
    {


        public readonly dbContext db;
        public readonly ICapPublisher cap;
        public readonly Guid userId;


        protected ControllerCore()
        {
            db = Http.HttpContext.Current().RequestServices.GetService<dbContext>();
            cap = Http.HttpContext.Current().RequestServices.GetService<ICapPublisher>();

            var userIdStr = Verify.JWTToken.GetClaims("userId");

            if (userIdStr != null)
            {
                userId = Guid.Parse(userIdStr);
            }
        }

    }
}
