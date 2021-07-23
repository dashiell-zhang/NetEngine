using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Repository.Database;
using System;
using System.Security.Claims;

namespace TaskAdmin.Controllers
{


    public class UserController : Controller
    {


        private readonly dbContext db;

        public UserController(dbContext context)
        {
            db = context;
        }


        public IActionResult Login(string returnUrl)
        {
            return View();
        }


        public JsonResult LoginAction(string name, string pwd)
        {
            var retValue = new { status = true };

            if (name == "admin" && pwd == "123456")
            {
                var userId = Guid.NewGuid();

                var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
                identity.AddClaim(new Claim("userId", userId.ToString()));
                HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

                HttpContext.Session.SetString("userId", userId.ToString());
            }
            else
            {
                retValue = new { status = false };
            }


            return Json(retValue);
        }

    }
}
