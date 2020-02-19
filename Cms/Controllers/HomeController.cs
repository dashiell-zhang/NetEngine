using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Cms.Filters;
using Microsoft.AspNetCore.Http;
using Cms.Libraries;
using Microsoft.Extensions.Configuration;
using Repository.WebCore;
using Cms.Models;

namespace Cms.Controllers
{

    [IsLogin]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {

            ViewBag.NickName = HttpContext.Session.GetString("nickname");
            ViewBag.UserId = HttpContext.Session.GetString("userid");


            using (var db = new webcoreContext())
            {
                IDictionary<string, object> list = new Dictionary<string, object>();
                var TChannel = db.TChannel.Where(t => t.IsDelete == false).OrderBy(t => t.Sort).ToList();
                list.Add("TChannel", TChannel);
                return View(list);
            }
        }


        public IActionResult Center()
        {

            ViewBag.LocalIpAddress = HttpContext.Connection.LocalIpAddress.MapToIPv4().ToString();
            ViewBag.LocalPort = HttpContext.Connection.LocalPort;

            return View();
        }


        public IActionResult WebInfo()
        {
            using (var db = new webcoreContext())
            {
                var webInfo = db.TWebInfo.FirstOrDefault();
                return View(webInfo);
            }
        }



        public bool WebInfoSave(TWebInfo webInfo)
        {

            using (var db = new webcoreContext())
            {
                var dbInfo = db.TWebInfo.FirstOrDefault();

                dbInfo.WebUrl = webInfo.WebUrl;
                dbInfo.ManagerName = webInfo.ManagerName;
                dbInfo.ManagerAddress = webInfo.ManagerAddress;
                dbInfo.ManagerPhone = webInfo.ManagerPhone;
                dbInfo.ManagerEmail = webInfo.ManagerEmail;
                dbInfo.RecordNumber = webInfo.RecordNumber;
                dbInfo.SeoTitle = webInfo.SeoTitle;
                dbInfo.SeoKeyWords = webInfo.SeoKeyWords;
                dbInfo.SeoDescription = webInfo.SeoDescription;
                dbInfo.FootCode = webInfo.FootCode;

                db.SaveChanges();

                return true;
            }
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
