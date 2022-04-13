using AdminApi.Filters;
using AdminApi.Libraries;
using AdminShared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Repository.Database;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;

namespace AdminApi.Controllers.v1
{

    /// <summary>
    /// 文件上传控制器
    /// </summary>
    [SignVerifyFilter]
    [ApiVersion("1")]
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class FileController : ControllerCore
    {




        /// <summary>
        /// 单文件上传接口
        /// </summary>
        /// <param name="business">业务领域</param>
        /// <param name="key">记录值</param>
        /// <param name="sign">自定义标记</param>
        /// <param name="file">file</param>
        /// <returns>文件ID</returns>
        [DisableRequestSizeLimit]
        [HttpPost("UploadFile")]
        public long UploadFile([FromQuery] string business, [FromQuery] long key, [FromQuery] string sign, IFormFile file)
        {

            string basepath = "/uploads/" + DateTime.UtcNow.ToString("yyyy/MM/dd");
            string filepath = Libraries.IO.Path.WebRootPath() + basepath;

            Directory.CreateDirectory(filepath);

            var fileName = snowflakeHelper.GetId();
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fullFileName = string.Format("{0}{1}", fileName, fileExtension);

            string path;

            var isSuccess = false;

            if (file != null && file.Length > 0)
            {
                path = filepath + "/" + fullFileName;

                using (FileStream fs = System.IO.File.Create(path))
                {
                    file.CopyTo(fs);
                    fs.Flush();
                }

                var upRemote = false;

                if (upRemote)
                {

                    var oss = new Common.AliYun.OssHelper();

                    var upload = oss.FileUpload(path, "uploads/" + DateTime.UtcNow.ToString("yyyy/MM/dd"), file.FileName);

                    if (upload)
                    {
                        Common.IOHelper.DeleteFile(path);

                        path = "/uploads/" + DateTime.UtcNow.ToString("yyyy/MM/dd") + "/" + fullFileName;
                        isSuccess = true;
                    }
                }
                else
                {
                    path = basepath + "/" + fullFileName;
                    isSuccess = true;
                }

                if (isSuccess)
                {

                    TFile f = new();
                    f.Id = fileName;
                    f.Name = file.FileName;
                    f.Path = path;
                    f.Table = business;
                    f.Sign = sign;
                    f.TableId = key;
                    f.CreateUserId = userId;
                    f.CreateTime = DateTime.UtcNow;
                    db.TFile.Add(f);
                    db.SaveChanges();

                    return fileName;
                }

            }

            HttpContext.Response.StatusCode = 400;
            HttpContext.Items.Add("errMsg", "文件上传失败");
            return default;
        }




        /// <summary>
        /// 通过文件ID获取文件
        /// </summary>
        /// <param name="fileid">文件ID</param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpGet("GetFile")]
        public FileResult? GetFile(long fileid)
        {

            var file = db.TFile.Where(t => t.Id == fileid).FirstOrDefault();

            if (file != null)
            {
                string path = Libraries.IO.Path.WebRootPath() + file.Path;


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
            else
            {
                return default;
            }
        }




        /// <summary>
        /// 通过图片文件ID获取图片
        /// </summary>
        /// <param name="fileId">图片ID</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <returns></returns>
        /// <remarks>不指定宽高参数,返回原图</remarks>
        [AllowAnonymous]
        [HttpGet("GetImage")]
        public FileResult? GetImage(long fileId, int width, int height)
        {
            var file = db.TFile.Where(t => t.Id == fileId).FirstOrDefault();

            if (file != null)
            {
                var path = Libraries.IO.Path.WebRootPath() + file.Path;

                string fileExt = Path.GetExtension(path);
                var provider = new FileExtensionContentTypeProvider();
                var memi = provider.Mappings[fileExt];

                using var fileStream = System.IO.File.OpenRead(path);

                if (width == 0 && height == 0)
                {
                    return File(fileStream, memi, file.Name);
                }
                else
                {

                    using var original = SKBitmap.Decode(path);
                    if (original.Width < width || original.Height < height)
                    {
                        return File(fileStream, memi, file.Name);
                    }
                    else
                    {

                        if (width != 0 && height == 0)
                        {
                            var percent = ((float)width / (float)original.Width);

                            width = (int)(original.Width * percent);
                            height = (int)(original.Height * percent);
                        }

                        if (width == 0 && height != 0)
                        {
                            var percent = ((float)height / (float)original.Height);

                            width = (int)(original.Width * percent);
                            height = (int)(original.Height * percent);
                        }

                        using var resizeBitmap = original.Resize(new SKImageInfo(width, height), SKFilterQuality.High);
                        using var image = SKImage.FromBitmap(resizeBitmap);
                        using var imageData = image.Encode(SKEncodedImageFormat.Png, 100);
                        return File(imageData.ToArray(), "image/png");
                    }
                }
            }
            else
            {
                return default;
            }

        }



        /// <summary>
        /// 通过文件ID获取文件静态访问路径
        /// </summary>
        /// <param name="fileid">文件ID</param>
        /// <returns></returns>
        [HttpGet("GetFilePath")]
        public string? GetFilePath(long fileid)
        {

            var file = db.TFile.AsNoTracking().Where(t => t.IsDelete == false && t.Id == fileid).FirstOrDefault();

            if (file != null)
            {
                var fileServerUrl = Common.IOHelper.GetConfig()["FileServerUrl"].ToString();

                string fileUrl = fileServerUrl + file.Path;

                return fileUrl;
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
                HttpContext.Items.Add("errMsg", "通过指定的文件ID未找到任何文件");

                return default;
            }

        }




        /// <summary>
        /// 通过文件ID删除文件方法
        /// </summary>
        /// <param name="id">文件ID</param>
        /// <returns></returns>
        [HttpDelete("DeleteFile")]
        public bool DeleteFile(long id)
        {

            var file = db.TFile.Where(t => t.IsDelete == false && t.Id == id).FirstOrDefault();

            if (file != null)
            {
                file.IsDelete = true;
                file.DeleteTime = DateTime.UtcNow;

                db.SaveChanges();

                return true;
            }
            else
            {
                return false;
            }

        }


    }
}