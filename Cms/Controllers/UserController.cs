using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Methods.Property;
using Models.WebCore;

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


            using (WebCoreContext db = new WebCoreContext())
            {
                var user = db.TUserSys.Where(t => t.Name == name & t.Password == pwd).FirstOrDefault();

                if (user != null)
                {
                    HttpContext.Session.SetInt32("userid", user.Id);
                    HttpContext.Session.SetString("nickname", user.Nickname);
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
         
            HttpContext.Session.SetInt32("userid", 0);
            HttpContext.Session.SetString("nickname", "");

            Response.Redirect("/User/Login/");
        }



        public IActionResult UserSys()
        {
            using (WebCoreContext db = new WebCoreContext())
            {
                IDictionary<string, object> list = new Dictionary<string, object>();
                var t_user = db.TUserSys.ToList();
                list.Add("t_user_sys", t_user);
                return View(list);
            }
        }


        public IActionResult UserSys_Edit(int id = 0)
        {

            if (id == 0)
            {
                return View(new TUserSys());
            }
            else
            {
                using (WebCoreContext db = new WebCoreContext())
                {
                    var UserSys = db.TUserSys.Where(t => t.Id == id).FirstOrDefault();
                    return View(UserSys);
                }
            }

        }


        public void UserSys_Edit_Run(TUserSys UserSys)
        {
            using (WebCoreContext db = new WebCoreContext())
            {


                if (UserSys.Id == 0)
                {
                    //执行添加

                    UserSys.Createtime = DateTime.Now;

                    db.TUserSys.Add(UserSys);
                }
                else
                {
                    //执行修改
                    var dbUserSys = db.TUserSys.Where(t => t.Id == UserSys.Id).FirstOrDefault();

                    PropertyHelper.Assignment<TUserSys>(dbUserSys, UserSys);

                    dbUserSys.Updatetime = DateTime.Now;

                }

                db.SaveChanges();

                Response.Redirect("/User/UserSys");
            }
        }


        public JsonResult UserSys_Delete(int id)
        {
            using (WebCoreContext db = new WebCoreContext())
            {
                TUserSys UserSys = db.TUserSys.Where(t => t.Id == id).FirstOrDefault();
                db.TUserSys.Remove(UserSys);
                db.SaveChanges();

                var data = new { ret = 1, msg = "删除成功！" };
                return Json(data);
            }

        }

    }

}