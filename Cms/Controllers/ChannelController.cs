using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Methods.Property;
using Microsoft.AspNetCore.Mvc;
using Models.WebCore;

namespace Cms.Controllers
{
    public class ChannelController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }


        [HttpGet]
        public JsonResult GetChannelList()
        {
            using (webcoreContext db = new webcoreContext())
            {
                IList<TChannel> list = db.TChannel.ToList();

                return Json(new { data = list });
            }
        }



        public IActionResult Channel_Edit(int id = 0)
        {

            if (id == 0)
            {
                return View(new TChannel());
            }
            else
            {
                using (webcoreContext db = new webcoreContext())
                {
                    var Channel = db.TChannel.Where(t => t.Id == id).FirstOrDefault();
                    return View(Channel);
                }
            }

        }


        public void Channel_Edit_Run(TChannel Channel)
        {
            using (webcoreContext db = new webcoreContext())
            {


                if (Channel.Id == 0)
                {
                    //执行添加

                    Channel.Createtime = DateTime.Now;

                    db.TChannel.Add(Channel);
                }
                else
                {
                    //执行修改
                    var dbChannel = db.TChannel.Where(t => t.Id == Channel.Id).FirstOrDefault();

                    PropertyHelper.Assignment<TChannel>(dbChannel, Channel);

                    dbChannel.Updatetime = DateTime.Now;

                }

                db.SaveChanges();

                Response.Redirect("/Channel/Index");
            }
        }


        public JsonResult Channel_Delete(int id)
        {
            using (webcoreContext db = new webcoreContext())
            {
                TChannel Channel = db.TChannel.Where(t => t.Id == id).FirstOrDefault();
                db.TChannel.Remove(Channel);
                db.SaveChanges();

                var data = new { status = true, msg = "删除成功！" };
                return Json(data);
            }

        }
    }
}