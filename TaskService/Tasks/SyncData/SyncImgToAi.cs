using Common.BaiduAi;
using Common.BaiduAi.Models.ImageClassify;
using Repository.Database;
using System;
using System.Linq;

namespace TaskService.Tasks.SyncData
{
    public class SyncImgToAi
    {

        public static void Run()
        {
            Add();
            Update();
            Screenshot();
        }


        private static void Add()
        {
            using (var db = new dbContext())
            {

                var imglist = db.TFile.Where(t => t.IsDelete == false & t.Table == "" & t.Sign == "" & db.TImgBaiduAI.Where(a => a.FileId == t.Id).Count() == 0).Select(t => new
                {
                    productid = t.TableId,
                    imgname = t.Name,
                    imgid = t.Id,
                }).Take(100).ToList();


                foreach (var img in imglist)
                {
                    var path = "D:/Products/" + img.imgname;

                    var info = new { imgid = img.imgid, productid = img.productid };



                    var result = ImageSearch.ProductAdd(path, Common.Json.JsonHelper.ObjectToJSON(info), 1, 1);

                    if (result != null)
                    {

                        var unique = result.Value<string>("cont_sign");

                        var imgai = db.TImgBaiduAI.Where(t => t.FileId == img.imgid).FirstOrDefault() ?? new TImgBaiduAI();

                        imgai.Unique = unique;
                        imgai.Result = result.ToString();

                        if (imgai.Id != default)
                        {
                            imgai.UpdateTime = DateTime.Now;
                        }
                        else
                        {
                            imgai.Id = Guid.NewGuid();
                            imgai.FileId = img.imgid;
                            imgai.CreateTime = DateTime.Now;

                            db.TImgBaiduAI.Add(imgai);
                        }

                        db.SaveChanges();

                    }
                }

            }
        }



        private static void Update()
        {
            using (var db = new dbContext())
            {
                var start = DateTime.Now.AddDays(-3);

                var imglist = db.TImgBaiduAI.Where(t => t.Unique != null & (t.UpdateTime ?? t.CreateTime) < start).Select(t => t.File).Select(t => new
                {
                    productid = t.TableId,
                    imgname = t.Name,
                    imgid = t.Id,
                }).Take(1000).ToList();


                foreach (var img in imglist)
                {
                    var path = "D:/Products/" + img.imgname;

                    var info = new { imgid = img.imgid, productid = img.productid };

                    var result = ImageSearch.ProductUpdate(path, Common.Json.JsonHelper.ObjectToJSON(info), 1, 1);

                    if (result != null)
                    {

                        var unique = result.Value<string>("cont_sign");

                        var imgai = db.TImgBaiduAI.Where(t => t.FileId == img.imgid).FirstOrDefault();

                        imgai.Result = result.ToString();

                        imgai.UpdateTime = DateTime.Now;

                        db.SaveChanges();
                    }

                }

            }
        }


        private static void Screenshot()
        {
            using (var db = new dbContext())
            {
                var imgList = db.TImgBaiduAI.Where(t => t.Result.Contains("\"error_code\": 216203")).Select(t => new { ImgName = t.File.Name, ImgId = t.FileId }).ToList();

                foreach (var img in imgList)
                {
                    var path = "D:/Products/" + img.ImgName;

                    var rt = ImageClassify.ObjectDetect(path);

                    try
                    {

                        var rtinfo = rt.GetValue("result").ToObject<ObjectDetect>();

                        var newpath = "D:/Products/screenshot_" + img.ImgName;

                        var screen = Common.ImgHelper.Screenshot(path, rtinfo.left, rtinfo.top, newpath, rtinfo.width, rtinfo.height);

                        if (screen)
                        {

                            var fileinfo = db.TFile.Where(t => t.Id == img.ImgId).FirstOrDefault();

                            fileinfo.Path = "screenshot_" + fileinfo.Path;


                            var imgai = db.TImgBaiduAI.Where(t => t.FileId == img.ImgId).FirstOrDefault();

                            db.TImgBaiduAI.Remove(imgai);

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
