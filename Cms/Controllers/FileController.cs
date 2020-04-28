using Cms.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Repository.Database;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Cms.Controllers
{

    [IsLogin]
    public class FileController : Controller
    {

        public JsonResult SetFileSort(string id, int sort)
        {
            using (var db = new dbContext())
            {

                var file = db.TFile.Where(t => t.Id == id).FirstOrDefault();

                if (file != null)
                {
                    file.Sort = sort;

                    db.SaveChanges();
                }

                var data = new { status = true, msg = "调整成功！" };
                return Json(data);
            }
        }



        [HttpPost]
        //[RequestSizeLimit(100_000_000)]
        [DisableRequestSizeLimit]
        public bool UploadFile(string Table, string TableId, string Sign)
        {
            try
            {

                var ReqFiles = Request.Form.Files;


                List<IFormFile> Attachments = new List<IFormFile>();
                for (int i = 0; i < ReqFiles.Count; i++)
                {
                    Attachments.Add(ReqFiles[i]);
                }

                var url = string.Empty;
                var fileName = string.Empty;
                var fileExtension = string.Empty;
                var fullFileName = string.Empty;

                string filepath = Libraries.IO.Path.WebRootPath() + "\\Upload\\" + DateTime.Now.ToString("yyyy-MM-dd");


                foreach (var file in Attachments)
                {
                    if (file != null)
                    {
                        Directory.CreateDirectory(filepath);

                        fileName = Guid.NewGuid().ToString();
                        fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                        fullFileName = string.Format("{0}{1}", fileName, fileExtension);
                        if (file != null && file.Length > 0)
                        {
                            string path = filepath + "\\" + fullFileName;

                            using (FileStream fs = System.IO.File.Create(path))
                            {
                                file.CopyTo(fs);
                                fs.Flush();
                            }


                            var upOss = false;

                            if (upOss)
                            {

                                var oss = new Common.AliYun.OssHelper();

                                var upload = oss.FileUpload(path, "Files/" + DateTime.Now.ToString("yyyyMMdd"));

                                if (upload)
                                {
                                    Common.IO.File.Delete(path);

                                    path = "/Files/" + DateTime.Now.ToString("yyyyMMdd") + "/" + fullFileName;
                                }
                            }
                            else
                            {
                                path = path.Replace(Libraries.IO.Path.WebRootPath(), "");
                            }



                            using (var db = new dbContext())
                            {
                                TFile fi = new TFile();
                                fi.Id = Guid.NewGuid().ToString();
                                fi.Name = file.FileName;
                                fi.Table = Table;
                                fi.TableId = TableId;
                                fi.Sign = Sign;
                                fi.Path = path;
                                fi.CreateTime = DateTime.Now;

                                db.TFile.Add(fi);

                                db.SaveChanges();
                            }
                        }

                    }

                }

                return true;

            }
            catch
            {
                return false;
            }
        }



        public bool DeleteFile(string id)
        {
            using (var db = new dbContext())
            {
                var userid = HttpContext.Session.GetString("userid");

                var file = db.TFile.Where(t => t.Id == id).FirstOrDefault();

                if (file != null)
                {
                    file.IsDelete = true;

                    db.SaveChanges();
                }

                return true;
            }
        }
    }
}
