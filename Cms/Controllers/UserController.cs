using Cms.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Repository.Database;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cms.Controllers
{
    [IsLogin]
    public class UserController : Controller
    {

        public IActionResult Index()
        {
            return View();
        }


        [IsLogin(IsSkip = true)]
        public IActionResult Login()
        {
            return View();
        }


        [IsLogin(IsSkip = true)]
        public JsonResult Login_Run(string name, string pwd)
        {
            var Data = new { status = true };


            using (var db = new dbContext())
            {
                var userlist = db.TUser.ToList();
                var user = db.TUser.Where(t => t.Name == name & t.PassWord == pwd & t.IsDelete == false).FirstOrDefault();

                if (user != null)
                {
                    HttpContext.Session.SetString("userid", user.Id.ToString());
                    HttpContext.Session.SetString("nickname", user.NickName);
                }
                else
                {
                    Data = new { status = false };
                }
            }

            return Json(Data);
        }


        //退出系统
        public void Login_Exit()
        {

            HttpContext.Session.SetString("userid", "");
            HttpContext.Session.SetString("nickname", "");

            Response.Redirect("/User/Login/");
        }



        public IActionResult UserIndex()
        {
            return View();
        }



        [HttpGet]
        public JsonResult GetUserList()
        {
            using (var db = new dbContext())
            {
                IList<TUser> list = db.TUser.Where(t => t.IsDelete == false).ToList();

                return Json(new { data = list });
            }
        }



        public IActionResult UserEdit(string id)
        {

            if (string.IsNullOrEmpty(id))
            {
                return View(new TUser());
            }
            else
            {
                using (var db = new dbContext())
                {
                    var UserSys = db.TUser.Where(t => t.Id == id).FirstOrDefault();
                    return View(UserSys);
                }
            }

        }


        public void UserSave(TUser user)
        {
            using (var db = new dbContext())
            {

                if (string.IsNullOrEmpty(user.Id))
                {
                    //执行添加
                    user.Id = Guid.NewGuid().ToString();
                    user.IsDelete = false;
                    user.CreateTime = DateTime.Now;

                    //user.RoleId = "";

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

                Response.Redirect("/User/UserIndex");
            }
        }


        public JsonResult UserDelete(string id)
        {
            using (var db = new dbContext())
            {
                var UserSys = db.TUser.Where(t => t.Id == id).FirstOrDefault();
                db.TUser.Remove(UserSys);
                db.SaveChanges();

                var data = new { status = true, msg = "删除成功！" };
                return Json(data);
            }

        }

    }

}