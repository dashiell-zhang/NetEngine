using Common.Img;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Models.Dtos;
using Repository.WebCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using WebApi.Filters;

namespace WebApi.Controllers
{

    /// <summary>
    /// 文件上传控制器
    /// </summary>
    [Authorize]
    [JwtTokenVerify]
    [Route("api/[controller]")]
    [ApiController]
    public class FileController : ControllerBase
    {



        /// <summary>
        /// 单文件上传接口
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="tableId">记录ID</param>
        /// <param name="sign">自定义标记</param>
        /// <param name="file">file</param>
        /// <returns>文件ID</returns>
        [HttpPost("UploadFile")]
        public string UploadFile([FromQuery][Required]string table, [FromQuery][Required]string tableId, [FromQuery][Required]string sign, [FromBody][Required]IFormFile file)
        {

            string userid = Libraries.Verify.JwtToken.GetClaims("userid");

            var url = string.Empty;
            var fileName = string.Empty;
            var fileExtension = string.Empty;
            var fullFileName = string.Empty;

            string basepath = "\\Files\\" + DateTime.Now.ToString("yyyyMMdd");
            string filepath = Libraries.IO.Path.ContentRootPath() + basepath;

            Directory.CreateDirectory(filepath);

            fileName = Guid.NewGuid().ToString();
            fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            fullFileName = string.Format("{0}{1}", fileName, fileExtension);

            string path = "";

            if (file != null && file.Length > 0)
            {
                path = filepath + "\\" + fullFileName;

                using (FileStream fs = System.IO.File.Create(path))
                {
                    file.CopyTo(fs);
                    fs.Flush();
                }

                path = basepath + "\\" + fullFileName;
            }

            using (webcoreContext db = new webcoreContext())
            {
                var f = new TFile();
                f.Id = fileName;
                f.Name = file.FileName;
                f.Path = path;
                f.Table = table;
                f.TableId = tableId;
                f.Sign = sign;
                f.CreateTime = DateTime.Now;
                db.TFile.Add(f);
                db.SaveChanges();
            }

            return fileName;

        }


        /// <summary>
        /// 多文件上传接口
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="tableId">记录ID</param>
        /// <param name="sign">标记</param>
        /// <returns></returns>
        /// <remarks>swagger 暂不支持多文件接口测试，请使用 postman</remarks>
        [HttpPost("BatchUploadFile")]
        public List<dtoFileInfo> BatchUploadFile([FromQuery][Required]string table, [FromQuery][Required]string tableId, [FromQuery][Required]string sign)
        {

            var fileInfos = new List<dtoFileInfo>();

            var ReqFiles = Request.Form.Files;

            string userid = Libraries.Verify.JwtToken.GetClaims("userid");

            List<IFormFile> Attachments = new List<IFormFile>();
            for (int i = 0; i < ReqFiles.Count; i++)
            {
                Attachments.Add(ReqFiles[i]);
            }

            foreach (var file in Attachments)
            {
                var url = string.Empty;
                var fileName = string.Empty;
                var fileExtension = string.Empty;
                var fullFileName = string.Empty;

                string basepath = "\\Files\\" + DateTime.Now.ToString("yyyyMMdd");
                string filepath = Libraries.IO.Path.ContentRootPath() + basepath;

                Directory.CreateDirectory(filepath);

                fileName = Guid.NewGuid().ToString();
                fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                fullFileName = string.Format("{0}{1}", fileName, fileExtension);

                string path = "";

                if (file != null && file.Length > 0)
                {
                    path = filepath + "\\" + fullFileName;

                    using (FileStream fs = System.IO.File.Create(path))
                    {
                        file.CopyTo(fs);
                        fs.Flush();
                    }

                    path = basepath + "\\" + fullFileName;
                }

                using (webcoreContext db = new webcoreContext())
                {
                    var f = new TFile();
                    f.Id = fileName;
                    f.Name = file.FileName;
                    f.Path = path;
                    f.Table = table;
                    f.TableId = tableId;
                    f.Sign = sign;
                    f.CreateTime = DateTime.Now;

                    db.TFile.Add(f);
                    db.SaveChanges();

                    var fileinfo = new dtoFileInfo();

                    fileinfo.fileid = f.Id;
                    fileinfo.filename = f.Name;

                    fileInfos.Add(fileinfo);
                }

            }

            return fileInfos;
        }



        /// <summary>
        /// 通过文件ID获取文件
        /// </summary>
        /// <param name="fileid">文件ID</param>
        /// <returns></returns>
        [AllowAnonymous]
        [JwtTokenVerify(IsSkip = true)]
        [HttpGet("GetFile")]
        public FileResult GetFile([Required]string fileid)
        {
            using (webcoreContext db = new webcoreContext())
            {
                var file = db.TFile.Where(t => t.Id == fileid).FirstOrDefault();
                string path = Libraries.IO.Path.ContentRootPath() + file.Path;


                //读取文件入流
                var stream = System.IO.File.OpenRead(path);

                //获取文件后缀
                string fileExt = Path.GetExtension(path);

                //获取系统常规全部mime类型
                var provider = new FileExtensionContentTypeProvider();

                //通过文件后缀寻找对呀的mime类型
                var memi = provider.Mappings.ContainsKey(fileExt) ? provider.Mappings[fileExt] : provider.Mappings[".zip"];


                return File(stream, memi, file.Name);

            }

        }



        /// <summary>
        /// 多文件切片上传，获取初始化文件ID
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="tableId">记录ID</param>
        /// <param name="sign">自定义标记</param>
        /// <param name="fileName">文件名称</param>
        /// <param name="slicing">总切片数</param>
        /// <param name="unique">文件校验值</param>
        /// <returns></returns>
        [HttpGet("CreateGroupFileId")]
        public string CreateGroupFileId([Required]string table, [Required]string tableId, [Required]string sign, [Required]string fileName, [Required] int slicing, [Required]string unique)
        {
            var userid = WebApi.Libraries.Verify.JwtToken.GetClaims("userid");
            using (webcoreContext db = new webcoreContext())
            {

                var dbfileinfo = db.TFileGroup.Where(t => t.Unique.ToLower() == unique.ToLower()).FirstOrDefault();

                if (dbfileinfo == null)
                {

                    var fileid = Guid.NewGuid().ToString() + Path.GetExtension(fileName).ToLowerInvariant(); ;

                    string basepath = "\\Files\\" + DateTime.Now.ToString("yyyyMMdd") + "\\" + fileid;


                    var f = new TFile();
                    f.Id = Guid.NewGuid().ToString();
                    f.Name = fileName;
                    f.Path = basepath;
                    f.Table = table;
                    f.TableId = tableId;
                    f.Sign = sign;
                    f.CreateTime = DateTime.Now;

                    db.TFile.Add(f);
                    db.SaveChanges();

                    var group = new TFileGroup();
                    group.Id = Guid.NewGuid().ToString();
                    group.FileId = f.Id;
                    group.Unique = unique;
                    group.Slicing = slicing;
                    group.Issynthesis = false;
                    group.Isfull = false;
                    db.TFileGroup.Add(group);
                    db.SaveChanges();

                    return f.Id;
                }
                else
                {
                    return "The file already exists, and the file ID is:" + dbfileinfo.FileId;
                }
            }
        }


        /// <summary>
        /// 文件切片上传接口
        /// </summary>
        /// <param name="fileId">文件组ID</param>
        /// <param name="index">切片索引</param>
        /// <param name="file">file</param>
        /// <returns>文件ID</returns>
        [HttpPost("UploadGroupFile")]
        public bool UploadGroupFile([Required][FromForm]string fileId, [Required][FromForm]int index, [Required]IFormFile file)
        {

            try
            {
                var url = string.Empty;
                var fileName = string.Empty;
                var fileExtension = string.Empty;
                var fullFileName = string.Empty;

                string basepath = "\\Files\\Group\\" + DateTime.Now.ToString("yyyyMMdd") + "\\" + fileId;
                string filepath = WebApi.Libraries.IO.Path.ContentRootPath() + basepath;

                Directory.CreateDirectory(filepath);

                fileName = Guid.NewGuid().ToString();
                fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                fullFileName = string.Format("{0}{1}", fileName, fileExtension);

                string path = "";

                if (file != null && file.Length > 0)
                {
                    path = filepath + "\\" + fullFileName;

                    using (FileStream fs = System.IO.File.Create(path))
                    {
                        file.CopyTo(fs);
                        fs.Flush();
                    }

                    path = basepath + "\\" + fullFileName;
                }

                using (webcoreContext db = new webcoreContext())
                {
                    var group = db.TFileGroup.Where(t => t.FileId == fileId).FirstOrDefault();

                    var groupfile = new TFileGroupFile();
                    groupfile.Id = Guid.NewGuid().ToString();
                    groupfile.FileId = group.FileId;
                    groupfile.Path = path;
                    groupfile.Index = index;
                    groupfile.CreateTime = DateTime.Now;

                    db.TFileGroupFile.Add(groupfile);

                    if (index == group.Slicing)
                    {
                        group.Isfull = true;
                    }

                    db.SaveChanges();

                    if (group.Isfull == true)
                    {

                        try
                        {
                            byte[] buffer = new byte[1024 * 100];

                            var fileinfo = db.TFile.Where(t => t.Id == fileId).FirstOrDefault();

                            var fullfilepath = WebApi.Libraries.IO.Path.ContentRootPath() + fileinfo.Path;

                            using (FileStream outStream = new FileStream(fullfilepath, FileMode.Create))
                            {
                                int readedLen = 0;
                                FileStream srcStream = null;

                                var filelist = db.TFileGroupFile.Where(t => t.FileId == fileinfo.Id).OrderBy(t => t.Index).ToList();

                                foreach (var item in filelist)
                                {
                                    string p = WebApi.Libraries.IO.Path.ContentRootPath() + item.Path;
                                    srcStream = new FileStream(p, FileMode.Open);
                                    while ((readedLen = srcStream.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        outStream.Write(buffer, 0, readedLen);
                                    }
                                    srcStream.Close();
                                }
                            }

                            group.Issynthesis = true;

                            db.SaveChanges();
                        }
                        catch
                        {

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

        /// <summary>
        /// 通过文件ID删除文件方法
        /// </summary>
        /// <param name="id">文件ID</param>
        /// <returns></returns>
        [HttpDelete("DeleteFile")]
        public bool DeleteFile(string id)
        {
            try
            {

                using (var db = new webcoreContext())
                {
                    var file = db.TFile.Where(t => t.IsDelete == false && t.Id == id).FirstOrDefault();

                    file.IsDelete = true;
                    file.DeleteTime = DateTime.Now;

                    db.SaveChanges();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }



        /// <summary>
        /// 自定义二维码生成方法
        /// </summary>
        /// <param name="text">数据内容</param>
        /// <returns></returns>
        [AllowAnonymous]
        [JwtTokenVerify(IsSkip = true)]
        [HttpGet("GetQrCode")]
        public FileResult GetQrCode(string text)
        {
            var image = QRCodeHelper.GetQrCode(text);
            MemoryStream ms = new MemoryStream();
            image.Save(ms, System.DrawingCore.Imaging.ImageFormat.Png);
            return File(ms.ToArray(), "image/png");
        }

    }
}