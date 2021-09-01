using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Repository.Database;
using System;
using System.Collections.Generic;
using System.Linq;


namespace Web.Areas.Admin.Controllers
{


    [Authorize]
    public class LinkController : Controller
    {


        private readonly dbContext db;

        public LinkController(dbContext context)
        {
            db = context;
        }


        public IActionResult LinkIndex()
        {
            return View();
        }


        public JsonResult GetLinkList()
        {

            var list = db.TLink.AsNoTracking().Where(t => t.IsDelete == false).OrderBy(t => t.Sort).ToList();

            return Json(new { data = list });
        }



        public IActionResult LinkEdit(Guid id)
        {

            if (id == default)
            {
                var link = new TLink();
                link.Id = Guid.NewGuid();

                ViewData["LinkInfo"] = link;

                ViewData["coverList"] = new List<TFile>();
                return View();
            }
            else
            {
                var Link = db.TLink.AsNoTracking().Where(t => t.IsDelete == false & t.Id == id).FirstOrDefault();

                ViewData["LinkInfo"] = Link;

                var coverList = db.TFile.AsNoTracking().Where(t => t.IsDelete == false && t.Sign == "cover" & t.Table == "TLink" & t.TableId == id).OrderBy(t => t.Sort).ToList();
                ViewData["coverList"] = coverList;

                return View();
            }
        }




        public bool LinkSave(TLink Link)
        {

            var userId = Guid.Parse(HttpContext.Session.GetString("userId"));

            var dbLink = db.TLink.Where(t => t.IsDelete == false & t.Id == Link.Id).FirstOrDefault();

            dbLink.Name = Link.Name;
            dbLink.Url = Link.Url;
            dbLink.Remarks = Link.Remarks;
            dbLink.Sort = Link.Sort;

            if (dbLink.Id == default)
            {
                //执行添加

                Link.Id = Guid.NewGuid();
                Link.CreateTime = DateTime.Now;
                Link.CreateUserId = userId;

                db.TLink.Add(Link);
            }

            db.SaveChanges();

            return true;

        }




        public JsonResult LinkDelete(Guid id)
        {
            var userId = Guid.Parse(HttpContext.Session.GetString("userId"));

            var link = db.TLink.Where(t => t.IsDelete == false & t.Id == id).FirstOrDefault();

            link.IsDelete = true;
            link.DeleteTime = DateTime.Now;
            link.DeleteUserId = userId;

            db.SaveChanges();

            var data = new { status = true, msg = "删除成功！" };
            return Json(data);
        }
    }
}
