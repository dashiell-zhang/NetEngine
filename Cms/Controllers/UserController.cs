using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Common.Property;
using Models.DataBases.WebCore;

namespace Cms.Controllers
{
    public class UserController : Controller
    {

        public IActionResult Index()
        {
            return View();
        }



        public IActionResult Login()
        {
            return View();
        }


        public JsonResult Login_Run(string name, string pwd)
        {
            var Data = new { status = true };


            using (webcoreContext db = new webcoreContext())
            {
                var user = db.TUser.Where(t => t.Name == name & t.PassWord == pwd).FirstOrDefault();

                if (user != null)
                {
                    HttpContext.Session.SetString("userid", user.Id);
                    HttpContext.Session.SetString("nickname", user.Name);
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



        public IActionResult UserSys()
        {
            return View();
        }



        [HttpGet]
        public JsonResult GetUserSysList()
        {
            using (webcoreContext db = new webcoreContext())
            {
                IList<TUser> list = db.TUser.ToList();

                return Json(new { data = list });
            }
        }



        public IActionResult UserSys_Edit(string id)
        {

            if (string.IsNullOrEmpty(id))
            {
                return View(new TUser());
            }
            else
            {
                using (webcoreContext db = new webcoreContext())
                {
                    var UserSys = db.TUser.Where(t => t.Id == id).FirstOrDefault();
                    return View(UserSys);
                }
            }

        }


        public void UserSys_Edit_Run(TUser UserSys)
        {
            using (webcoreContext db = new webcoreContext())
            {


                if (string.IsNullOrEmpty(UserSys.Id))
                {
                    //执行添加

                    UserSys.CreateTime = DateTime.Now;

                    db.TUser.Add(UserSys);
                }
                else
                {
                    //执行修改
                    var dbUserSys = db.TUser.Where(t => t.Id == UserSys.Id).FirstOrDefault();

                    PropertyHelper.Assignment<TUser>(dbUserSys, UserSys);

                    dbUserSys.UpdateTime = DateTime.Now;

                }

                db.SaveChanges();

                Response.Redirect("/User/UserSys");
            }
        }


        public JsonResult UserSys_Delete(string id)
        {
            using (webcoreContext db = new webcoreContext())
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