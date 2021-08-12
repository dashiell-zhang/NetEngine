using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Repository.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace Cms.Controllers
{
    [Authorize]
    public class UserController : Controller
    {

        private readonly dbContext db;

        public UserController(dbContext context)
        {
            db = context;
        }



        [AllowAnonymous]
        public IActionResult Login(string returnUrl)
        {
            return View();
        }


        [AllowAnonymous]
        public JsonResult LoginAction(string name, string pwd)
        {
            var retValue = new { status = true };

            var user = db.TUser.Where(t => t.IsDelete == false & t.Name == name & t.PassWord == pwd).FirstOrDefault();

            if (user != null)
            {
                var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
                identity.AddClaim(new Claim("userId", user.Id.ToString()));
                HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

                HttpContext.Session.SetString("userId", user.Id.ToString());
                HttpContext.Session.SetString("nickName", user.NickName);
            }
            else
            {
                retValue = new { status = false };
            }

            return Json(retValue);
        }



        public void Logout()
        {
            HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            HttpContext.Session.Remove("userId");
            HttpContext.Session.Remove("nickName");

            Response.Redirect("/User/Login/");
        }



        public IActionResult UserIndex()
        {
            return View();
        }



        [HttpGet]
        public JsonResult GetUserList()
        {

            IList<TUser> list = db.TUser.Where(t => t.IsDelete == false).ToList();

            return Json(new { data = list });
        }



        public IActionResult UserEdit(Guid id)
        {

            if (id == default)
            {
                return View(new TUser());
            }
            else
            {

                var UserSys = db.TUser.Where(t => t.Id == id).FirstOrDefault();
                return View(UserSys);
            }

        }


        public bool UserSave(TUser user)
        {


            if (user.Id == default)
            {
                //执行添加
                user.Id = Guid.NewGuid();
                user.IsDelete = false;
                user.CreateTime = DateTime.Now;

                db.TUser.Add(user);
            }
            else
            {
                //执行修改
                var dbUserSys = db.TUser.Where(t => t.Id == user.Id).FirstOrDefault();

                dbUserSys.Name = user.Name;
                dbUserSys.NickName = user.NickName;
                dbUserSys.Phone = user.Phone;
                dbUserSys.Email = user.Email;
                dbUserSys.PassWord = user.PassWord;
            }

            db.SaveChanges();


            return true;
        }


        public JsonResult UserDelete(Guid id)
        {

            var user = db.TUser.Where(t => t.Id == id).FirstOrDefault();

            user.IsDelete = true;
            user.DeleteTime = DateTime.Now;

            db.SaveChanges();

            var data = new { status = true, msg = "删除成功！" };
            return Json(data);

        }

    }

}