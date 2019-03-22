using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Cms.Models;
using Cms.Filters;
using Microsoft.AspNetCore.Http;
using Models.WebCore;
using Cms.Libraries;

namespace Cms.Controllers
{

    [IsLogin]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
       

            ViewBag.NickName = HttpContext.Session.GetString("nickname");
            ViewBag.UserId = HttpContext.Session.GetInt32("userid");


            using (WebCoreContext db = new WebCoreContext())
            {
                IDictionary<string, object> list = new Dictionary<string, object>();
                var TChannel = db.TChannel.ToList();
                list.Add("TChannel", TChannel);
                return View(list);
            }

        }


        public IActionResult Center()
        {

            ViewBag.LocalIpAddress = HttpContext.Connection.LocalIpAddress.MapToIPv4().ToString();
            ViewBag.LocalPort = HttpContext.Connection.LocalPort;
            ViewBag.Framework = Microsoft.Extensions.DependencyModel.DependencyContext.Default.Target.Framework;


            return View();
        }




        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
