using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repository.Database;

namespace TaskAdmin.Controllers
{

    [Authorize]
    public class HomeController : Controller
    {


        public void Index()
        {
            //进行任务注册
            Tasks.Main.Run();

            Response.Redirect("hangfire");
        }

    }
}
