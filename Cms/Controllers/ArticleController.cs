using Cms.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using Repository.WebCore;

namespace Cms.Controllers
{

    [IsLogin]
    public class ArticleController : Controller
    {
        public IActionResult ChannelIndex()
        {
            return View();
        }


        public JsonResult GetChannelList()
        {
            using (var db = new webcoreContext())
            {

                var list = db.TChannel.Where(t => t.IsDelete == false).OrderBy(t => t.Sort).ToList();

                return Json(new { data = list });
            }
        }


        public IActionResult ChannelEdit(string id = null)
        {

            if (id == null)
            {
                return View(new TChannel());
            }
            else
            {
                using (var db = new webcoreContext())
                {
                    var Channel = db.TChannel.Where(t => t.Id == id).FirstOrDefault();
                    return View(Channel);
                }
            }

        }


        public void ChannelSave(TChannel Channel)
        {
            using (var db = new webcoreContext())
            {

                if (string.IsNullOrEmpty(Channel.Id))
                {
                    //执行添加
                    Channel.Id = Guid.NewGuid().ToString();

                    var userid = HttpContext.Session.GetString("userid");

                    Channel.CreateTime = DateTime.Now;
                    Channel.CreateUserId = userid;
                    Channel.IsDelete = false;

                    db.TChannel.Add(Channel);
                }
                else
                {
                    //执行修改
                    var dbChannel = db.TChannel.Where(t => t.Id == Channel.Id).FirstOrDefault();

                    dbChannel.Name = Channel.Name;
                    dbChannel.Remark = Channel.Remark;
                    dbChannel.Sort = Channel.Sort;

                    var userid = HttpContext.Session.GetString("userid");
                }

                db.SaveChanges();

                Response.Redirect("/Article/ChannelIndex");
            }
        }


        public JsonResult ChannelDelete(string id)
        {
            using (var db = new webcoreContext())
            {
                var Channel = db.TChannel.Where(t => t.Id == id).FirstOrDefault();
                Channel.IsDelete = true;
                Channel.DeleteTime = DateTime.Now;
                Channel.DeleteUserId = HttpContext.Session.GetString("userid");

                db.SaveChanges();

                var data = new { status = true, msg = "删除成功！" };
                return Json(data);
            }

        }


        public IActionResult CategoryIndex(string ChannelId)
        {
            ViewBag.ChannelId = ChannelId;
            return View();
        }


        public JsonResult GetCategoryList(string ChannelId)
        {

            using (var db = new webcoreContext())
            {
                var list = db.TCategory.Where(t => t.ChannelId == ChannelId && t.IsDelete == false).Select(t => new { t.Id, t.Name, t.Remark, ParentName = t.Parent.Name, t.Sort, t.CreateTime }).ToList();

                return Json(new { data = list });
            }
        }


        public IActionResult CategoryEdit(string channelid, string id = null)
        {

            using (var db = new webcoreContext())
            {

                IDictionary<string, object> list = new Dictionary<string, object>();

                var categoryList = db.TCategory.Where(t => t.IsDelete == false && t.ChannelId == channelid).OrderBy(t => t.Sort).ToList();

                list.Add("categoryList", categoryList);


                if (string.IsNullOrEmpty(id))
                {
                    var category = new TCategory();
                    category.ChannelId = channelid;
                    list.Add("categoryInfo", category);
                }
                else
                {
                    var Category = db.TCategory.Where(t => t.Id == id).FirstOrDefault();
                    list.Add("categoryInfo", Category);
                }

                return View(list);
            }

        }


        public void CategorySave(TCategory Category)
        {

            if (Category.ParentId == "")
            {
                Category.ParentId = null;
            }

            var userid = HttpContext.Session.GetString("userid");

            using (var db = new webcoreContext())
            {

                if (string.IsNullOrEmpty(Category.Id))
                {
                    //执行添加

                    Category.Id = Guid.NewGuid().ToString();

                    Category.CreateTime = DateTime.Now;
                    Category.CreateUserId = userid;
                    Category.IsDelete = false;

                    db.TCategory.Add(Category);
                }
                else
                {
                    //执行修改
                    var dbCategory = db.TCategory.Where(t => t.Id == Category.Id).FirstOrDefault();

                    dbCategory.ParentId = Category.ParentId;
                    dbCategory.Name = Category.Name;
                    dbCategory.Remark = Category.Remark;
                    dbCategory.Sort = Category.Sort;

                }

                db.SaveChanges();

                Response.Redirect("/Article/CategoryIndex?channelid=" + Category.ChannelId);
            }
        }


        public JsonResult CategoryDelete(string id)
        {
            using (var db = new webcoreContext())
            {

                var subCategoryCount = db.TCategory.Where(t => t.ParentId == id && t.IsDelete == false).Count();

                if (subCategoryCount == 0)
                {
                    var userid = HttpContext.Session.GetString("userid");

                    var Category = db.TCategory.Where(t => t.Id == id).FirstOrDefault();

                    Category.IsDelete = true;
                    Category.DeleteUserId = userid;
                    Category.DeleteTime = DateTime.Now;

                    var articleList = db.TArticle.Where(t => t.CategoryId == id).ToList();

                    foreach (var article in articleList)
                    {
                        article.IsDelete = true;
                        article.DeleteUserId = userid;
                        article.DeleteTime = DateTime.Now;
                    }


                    db.SaveChanges();
                    var data = new { status = true, msg = "删除成功！" };
                    return Json(data);
                }
                else
                {
                    var data = new { status = false, msg = "该类别下存在子级分类无法直接删除，请先删除子级分类！" };
                    return Json(data);
                }

            }

        }


        public IActionResult ArticleIndex(string ChannelId)
        {
            ViewBag.ChannelId = ChannelId;
            return View();
        }


        public JsonResult GetArticleList(string ChannelId)
        {

            using (var db = new webcoreContext())
            {
                var list = db.TArticle.Where(t => t.Category.ChannelId == ChannelId && t.IsDelete == false).Select(t => new { t.Id, t.Title, CategoryName = t.Category.Name, t.Category.ChannelId, t.IsDisplay, t.IsRecommend, t.ClickCount, t.CreateTime }).ToList();

                return Json(new { data = list });
            }
        }


        public IActionResult ArticleEdit(string channelid, string id = null)
        {


            using (var db = new webcoreContext())
            {
                ViewData["Tenantid"] = HttpContext.Session.GetString("tenantid");


                var categoryList = db.TCategory.Where(t => t.IsDelete == false && t.ChannelId == channelid).OrderBy(t => t.Sort).ToList();

                ViewData["categoryList"] = categoryList;


                if (string.IsNullOrEmpty(id))
                {
                    var article = new TArticle();

                    article.Id = Guid.NewGuid().ToString();
                    article.IsDisplay = true;

                    ViewData["article"] = article;
                    ViewData["coverList"] = new List<TFile>();
                }
                else
                {
                    var article = db.TArticle.Where(t => t.Id == id).FirstOrDefault();
                    ViewData["article"] = article;

                    var coverList = db.TFile.Where(t => t.IsDelete == false && t.Sign == "cover" & t.Table == "TArticle" & t.TableId == id).OrderBy(t => t.Sort).ToList();
                    ViewData["coverList"] = coverList;

                }

                ViewData["channelid"] = channelid;

                return View();
            }

        }


        public bool ArticleSave(TArticle article)
        {
            try
            {
                string id = "";

                var userid = HttpContext.Session.GetString("userid");

                using (var db = new webcoreContext())
                {

                    if (db.TArticle.Where(t => t.Id == article.Id).FirstOrDefault() == null)
                    {
                        //执行添加

                        article.CreateTime = DateTime.Now;
                        article.CreateUserId = userid;
                        article.IsDelete = false;

                        db.TArticle.Add(article);

                        id = article.Id;

                    }
                    else
                    {
                        //执行修改
                        var dbArticle = db.TArticle.Where(t => t.Id == article.Id).FirstOrDefault();

                        dbArticle.CategoryId = article.CategoryId;
                        dbArticle.Title = article.Title;
                        dbArticle.Abstract = article.Abstract;
                        dbArticle.Content = article.Content;
                        dbArticle.IsDisplay = article.IsDisplay;
                        dbArticle.IsRecommend = article.IsRecommend;
                        dbArticle.ClickCount = article.ClickCount;

                        id = dbArticle.Id;

                    }

                    db.SaveChanges();

                    
                }

                return true;
            }
            catch
            {
                return false;
            }
        }


        public JsonResult ArticleDelete(string id)
        {
            using (var db = new webcoreContext())
            {
                var userid = HttpContext.Session.GetString("userid");

                var article = db.TArticle.Where(t => t.Id == id).FirstOrDefault();

                article.IsDelete = true;
                article.DeleteUserId = userid;
                article.DeleteTime = DateTime.Now;


                db.SaveChanges();

                var data = new { status = true, msg = "删除成功！" };
                return Json(data);
            }

        }

    }
}
