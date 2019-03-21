using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Methods.Property;
using Microsoft.AspNetCore.Mvc;
using Models.WebCore;

namespace Cms.Controllers
{
    public class CategoryController : Controller
    {
        public IActionResult Index(int ChannelId)
        {
            ViewBag.ChannelId = ChannelId;
            return View();
        }


        [HttpGet]
        public JsonResult GetCategoryList(int ChannelId)
        {
            using (WebCoreContext db = new WebCoreContext())
            {
                IList<TCategory> list = db.TCategory.Where(t => t.Channelid == ChannelId).ToList();

                return Json(new { data = list });
            }
        }



        public IActionResult Category_Edit(int channelid,int id = 0)
        {

            using (WebCoreContext db = new WebCoreContext())
            {

                IDictionary<string, object> list = new Dictionary<string, object>();

                IList<TCategory> categoryList = db.TCategory.Where(t=>t.Channelid==channelid).OrderBy(t => t.Sort).ToList();

                list.Add("categoryList", categoryList);
                

                if (id == 0)
                {
                    list.Add("categoryInfo", new TCategory());
                }
                else
                {
                    var Category = db.TCategory.Where(t => t.Id == id).FirstOrDefault();
                    list.Add("categoryInfo", Category);
                }

                return View(list);
            }

        }


        public void Category_Edit_Run(TCategory Category)
        {
            using (WebCoreContext db = new WebCoreContext())
            {

                if (Category.Id == 0)
                {
                    //执行添加

                    Category.Createtime = DateTime.Now;

                    db.TCategory.Add(Category);
                }
                else
                {
                    //执行修改
                    var dbCategory = db.TCategory.Where(t => t.Id == Category.Id).FirstOrDefault();

                    PropertyHelper.Assignment<TCategory>(dbCategory, Category);

                    dbCategory.Updatetime = DateTime.Now;

                }

                db.SaveChanges();

                Response.Redirect("/Category/Index?channelid="+Category.Channelid);
            }
        }


        public JsonResult Category_Delete(int id)
        {
            using (WebCoreContext db = new WebCoreContext())
            {
                TCategory Category = db.TCategory.Where(t => t.Id == id).FirstOrDefault();
                db.TCategory.Remove(Category);
                db.SaveChanges();

                var data = new { status = true, msg = "删除成功！" };
                return Json(data);
            }

        }
    }
}