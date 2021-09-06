using Cms.Libraries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Repository.Database;
using System;
using System.Linq;

namespace Cms.Controllers
{

    [Authorize]
    public class HomeController : ControllerCore
    {


        public IActionResult Index()
        {

            ViewBag.NickName = HttpContext.Session.GetString("nickName");
            ViewBag.UserId = userId;

            var channelList = db.TChannel.AsNoTracking().Where(t => t.IsDelete == false).OrderBy(t => t.Sort).ToList();

            ViewData["channelList"] = channelList;

            return View();
        }


        public IActionResult Center()
        {

            ViewBag.LocalIpAddress = HttpContext.Connection.LocalIpAddress.ToString();
            ViewBag.LocalPort = HttpContext.Connection.LocalPort;

            return View();
        }


        public IActionResult WebInfo()
        {

            var webInfo = db.TWebInfo.AsNoTracking().FirstOrDefault() ?? new TWebInfo();

            if (webInfo.Id == default)
            {
                webInfo.Id = Guid.NewGuid();

                db.TWebInfo.Add(webInfo);
                db.SaveChanges();
            }

            return View(webInfo);
        }



        public bool WebInfoSave(TWebInfo webInfo)
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
