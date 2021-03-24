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
    public class HomeController : Controller
    {
        public IActionResult Index()
        {

            ViewBag.NickName = HttpContext.Session.GetString("nickname");
            ViewBag.UserId = HttpContext.Session.GetString("userid");


            using (var db = new dbContext())
            {
                IDictionary<string, object> list = new Dictionary<string, object>();
                var TChannel = db.TChannel.Where(t => t.IsDelete == false).OrderBy(t => t.Sort).ToList();
                list.Add("TChannel", TChannel);
                return View(list);
            }
        }


        public IActionResult Center()
        {

            ViewBag.LocalIpAddress = HttpContext.Connection.LocalIpAddress.ToString();
            ViewBag.LocalPort = HttpContext.Connection.LocalPort;

            return View();
        }


        public IActionResult WebInfo()
        {
            using (var db = new dbContext())
            {
                var webInfo = db.TWebInfo.FirstOrDefault() ?? new TWebInfo();

                if (webInfo.Id == default)
                {
                    webInfo.Id = Guid.NewGuid();
                    db.TWebInfo.Add(webInfo);
                    db.SaveChanges();
                }

                return View(webInfo);
            }
        }



        public bool WebInfoSave(TWebInfo webInfo)
        {

            using (var db = new dbContext())
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

    }
}
