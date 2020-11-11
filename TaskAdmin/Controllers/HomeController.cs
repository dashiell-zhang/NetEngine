using Microsoft.AspNetCore.Mvc;
using TaskAdmin.Filters;

namespace TaskAdmin.Controllers
{

    public class HomeController : Controller
    {

        [IsLogin]
        public void Index()
        {
            //进行任务注册
            Tasks.Main.Run();

            Response.Redirect("hangfire");
        }

    }
}
