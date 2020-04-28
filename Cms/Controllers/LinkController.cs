using Cms.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Repository.Database;
using System;
using System.Collections.Generic;
using System.Linq;


namespace Web.Areas.Admin.Controllers
{


    [IsLogin]
    public class LinkController : Controller
    {
        public IActionResult LinkIndex()
        {
            return View();
        }


        public JsonResult GetLinkList()
        {
            using (var db = new dbContext())
            {

                var list = db.TLink.Where(t => t.IsDelete == false).OrderBy(t => t.Sort).ToList();

                return Json(new { data = list });
            }
        }



        public IActionResult LinkEdit(string id = null)
        {

            if (id == null)
            {
                var link = new TLink();
                link.Id = Guid.NewGuid().ToString();

                ViewData["LinkInfo"] = link;

                ViewData["coverList"] = new List<TFile>();
                return View();
            }
            else
            {
                using (var db = new dbContext())
                {
                    var Link = db.TLink.Where(t => t.Id == id).FirstOrDefault();

                    ViewData["LinkInfo"] = Link;

                    var coverList = db.TFile.Where(t => t.IsDelete == false && t.Sign == "cover" & t.Table == "TLink" & t.TableId == id).OrderBy(t => t.Sort).ToList();
                    ViewData["coverList"] = coverList;


                    return View();
                }
            }
        }


        public bool LinkSave(TLink Link)
        {
            try
            {
                using (var db = new dbContext())
                {
                    var nid = db.TLink.Where(t => t.Id == Link.Id).Select(t => t.Id).FirstOrDefault();

                    if (string.IsNullOrEmpty(nid))
                    {
                        //执行添加

                        var userid = HttpContext.Session.GetString("userid");

                        Link.CreateTime = DateTime.Now;
                        Link.CreateUserId = userid;
                        Link.IsDelete = false;

                        db.TLink.Add(Link);
                    }
                    else
                    {
                        //执行修改
                        var dbLink = db.TLink.Where(t => t.Id == Link.Id).FirstOrDefault();

                        dbLink.Name = Link.Name;
                        dbLink.Url = Link.Url;
                        dbLink.Remark = Link.Remark;
                        dbLink.Sort = Link.Sort;
                    }

                    db.SaveChanges();

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }


        public JsonResult LinkDelete(string id)
        {
            using (var db = new dbContext())
            {
                var Link = db.TLink.Where(t => t.Id == id).FirstOrDefault();
                Link.IsDelete = true;
                Link.DeleteTime = DateTime.Now;
                Link.DeleteUserId = HttpContext.Session.GetString("userid");

                db.SaveChanges();

                var data = new { status = true, msg = "删除成功！" };
                return Json(data);
            }

        }
    }
}
