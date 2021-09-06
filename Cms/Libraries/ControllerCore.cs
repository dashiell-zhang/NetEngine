using DotNetCore.CAP;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using System;

namespace Cms.Libraries
{
    public class ControllerCore : Controller
    {

        public readonly dbContext db;
        public readonly ICapPublisher cap;
        public readonly Guid userId;



        protected ControllerCore()
        {
            db = Http.HttpContext.Current().RequestServices.GetService<dbContext>();
            cap = Http.HttpContext.Current().RequestServices.GetService<ICapPublisher>();

            var userIdStr = Http.HttpContext.Current().Session.GetString("userId");

            if (userIdStr != null)
            {
                userId = Guid.Parse(userIdStr);
            }
        }
    }
}
