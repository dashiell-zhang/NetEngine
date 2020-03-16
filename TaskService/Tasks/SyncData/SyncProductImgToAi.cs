using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Repository.WebCore;
using Common.BaiduAi;
using Common.UseDB;
using Models.BaiduAi.ImageClassify;

namespace TaskService.Tasks.SyncData
{
    public class SyncProductImgToAi
    {

        public static void Run()
        {
            Add();
            Update();
            Screenshot();
        }


        private static void Add()
        {
            using (var db = new webcoreContext())
            {

                var imglist = db.TProductImg.Where(t => t.ProductImgBaiduAis.Count() == 0).Select(t => new
                {
                    productid = t.ProductId,
                    imgname = t.File.Name,
                    imgid = t.Id,
                }).Take(100).ToList();


                foreach (var img in imglist)
                {
                    var path = "D:\\Products\\" + img.imgname;

                    var info = new { imgid = img.imgid, productid = img.productid };



                    var result = ImageSearch.ProductAdd(path, Common.Json.JsonHelper.ObjectToJSON(info), 1, 1);

                    if (result != null)
                    {

                        var unique = result.Value<string>("cont_sign");

                        var imgai = db.TProductImgBaiduAi.Where(t => t.ProductImgId == img.imgid).FirstOrDefault() ?? new TProductImgBaiduAi();

                        imgai.Unique = unique;
                        imgai.Result = result.ToString();

                        if (!string.IsNullOrEmpty(imgai.Id))
                        {
                            imgai.UpdateTime = DateTime.Now;
                        }
                        else
                        {
                            imgai.Id = Guid.NewGuid().ToString();
                            imgai.ProductImgId = img.imgid;
                            imgai.CreateTime = DateTime.Now;

                            db.TProductImgBaiduAi.Add(imgai);
                        }

                        db.SaveChanges();

                    }
                }

            }
        }



        private static void Update()
        {
            using (var db = new webcoreContext())
            {
                var start = DateTime.Now.AddDays(-3);

                var imglist = db.TProductImgBaiduAi.Where(t => t.Unique != null & (t.UpdateTime ?? t.CreateTime) < start).Select(t => t.ProductImg).Select(t => new
                {
                    productid = t.ProductId,
                    imgname = t.File.Name,
                    imgid = t.Id,
                }).Take(1000).ToList();


                foreach (var img in imglist)
                {
                    var path = "D:\\Products\\" + img.imgname;

                    var info = new { imgid = img.imgid, productid = img.productid };

                    var result = ImageSearch.ProductUpdate(path, Common.Json.JsonHelper.ObjectToJSON(info), 1, 1);

                    if (result != null)
                    {

                        var unique = result.Value<string>("cont_sign");

                        var imgai = db.TProductImgBaiduAi.Where(t => t.ProductImgId == img.imgid).FirstOrDefault();

                        imgai.Result = result.ToString();

                        imgai.UpdateTime = DateTime.Now;

                        db.SaveChanges();
                    }

                }

            }
        }


        private static void Screenshot()
        {
            using (var db = new webcoreContext())
            {
                var imgList = db.TProductImgBaiduAi.Where(t => t.Result.Contains("\"error_code\": 216203")).Select(t => new { imgid = t.ProductImgId, imgname = t.ProductImg.File.Name, fileid = t.ProductImg.FileId }).ToList();

                foreach (var img in imgList)
                {
                    var path = "D:\\Products\\" + img.imgname;

                    var rt = ImageClassify.ObjectDetect(path);

                    try
                    {

                        var rtinfo = rt.GetValue("result").ToObject<ObjectDetect>();

                        var newpath = "D:\\Products\\screenshot_" + img.imgname;

                        var screen = Common.Img.Screenshot.Run(path, rtinfo.left, rtinfo.top, newpath, rtinfo.width, rtinfo.height);

                        if (screen)
                        {

                            var fileinfo = db.TFile.Where(t => t.Id == img.fileid).FirstOrDefault();

                            fileinfo.Path = "screenshot_" + fileinfo.Path;


                            var imgai = db.TProductImgBaiduAi.Where(t => t.ProductImgId == img.imgid).FirstOrDefault();

                            db.TProductImgBaiduAi.Remove(imgai);

                            db.SaveChanges();

                        }

                    }
                    catch
                    {

                    }

                }
            }
        }

    }
}
