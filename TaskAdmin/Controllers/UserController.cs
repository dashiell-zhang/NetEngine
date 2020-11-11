using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TaskAdmin.Controllers
{


    public class UserController : Controller
    {


        public IActionResult Index()
        {
            return View();
        }


        public JsonResult Login(string name, string pwd)
        {
            var Data = new { status = true };


            if (name == "admin" && pwd == "123456")
            {
                HttpContext.Session.SetString("userid", "admin");
            }
            else
            {
                Data = new { status = false };
            }


            return Json(Data);
        }

    }
}
