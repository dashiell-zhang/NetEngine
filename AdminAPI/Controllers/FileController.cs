using AdminAPI.Filters;
using AdminAPI.Libraries;
using Common;
using FileStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Repository.Database;
using SkiaSharp;

namespace AdminAPI.Controllers
{

    /// <summary>
    /// 文件上传控制器
    /// </summary>
    [SignVerifyFilter]
    [Authorize]
    [Route("[controller]")]
    [ApiController]
    public class FileController : ControllerBase
    {


        private readonly DatabaseContext db;
        private readonly IConfiguration configuration;
        private readonly IDHelper idHelper;
        private readonly IFileStorage? fileStorage;

        private readonly string rootPath;

        private readonly long userId;


        public FileController(DatabaseContext db, IConfiguration configuration, IDHelper idHelper, IWebHostEnvironment webHostEnvironment, IHttpContextAccessor httpContextAccessor, IFileStorage? fileStorage = null)
        {
            this.db = db;
            this.configuration = configuration;
            this.idHelper = idHelper;
            this.fileStorage = fileStorage;

            rootPath = webHostEnvironment.WebRootPath;

            var userIdStr = httpContextAccessor.HttpContext?.GetClaimByAuthorization("userId");

            if (userIdStr != null)
            {
                userId = long.Parse(userIdStr);
            }
        }



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
            var utcNow = DateTime.UtcNow;

            string basePath = Path.Combine("uploads", utcNow.ToString("yyyy"), utcNow.ToString("MM"), utcNow.ToString("dd"));

            string folderPath = Path.Combine(rootPath, basePath);

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var fileName = idHelper.GetId() + Path.GetExtension(file.FileName).ToLower();

            var filePath = Path.Combine(folderPath, fileName);

            var isSuccess = true;

            if (file.Length > 0)
            {

                using (FileStream fs = System.IO.File.Create(filePath))
                {
                    file.CopyTo(fs);
                    fs.Flush();
                }


                if (fileStorage != null)
                {
                    isSuccess = fileStorage.FileUpload(filePath, basePath, file.FileName);

                    if (isSuccess)
                    {
                        IOHelper.DeleteFile(filePath);
                    }
                }


                if (isSuccess)
                {

                    filePath = Path.Combine(basePath, fileName).Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    TFile f = new()
                    {
                        Id = idHelper.GetId(),
                        Name = file.FileName,
                        Path = filePath,
                        Table = business,
                        TableId = key,
                        Sign = sign,
                        CreateUserId = userId,
                        CreateTime = DateTime.UtcNow
                    };
                    db.TFile.Add(f);
                    db.SaveChanges();

                    return f.Id;
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
                string physicalPath = Path.Combine(rootPath, file.Path); ;

                string fileExt = Path.GetExtension(file.Path);

                FileExtensionContentTypeProvider provider = new();

                var memi = provider.Mappings.TryGetValue(fileExt, out string? value) ? value : provider.Mappings[".zip"];

                return PhysicalFile(physicalPath, memi, file.Name);
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
                HttpContext.Items.Add("errMsg", "通过指定的文件ID未找到任何文件");

                return null;
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
                var physicalPath = Path.Combine(rootPath, file.Path);

                string fileExt = Path.GetExtension(file.Path);
                FileExtensionContentTypeProvider provider = new();
                var memi = provider.Mappings[fileExt];



                if (width == 0 && height == 0)
                {
                    return PhysicalFile(physicalPath, memi, file.Name);
                }
                else
                {

                    using var original = SKBitmap.Decode(physicalPath);
                    if (original.Width < width || original.Height < height)
                    {
                        return PhysicalFile(physicalPath, memi, file.Name);
                    }
                    else
                    {

                        if (width != 0 && height == 0)
                        {
                            var percent = width / (float)original.Width;

                            width = (int)(original.Width * percent);
                            height = (int)(original.Height * percent);
                        }

                        if (width == 0 && height != 0)
                        {
                            var percent = height / (float)original.Height;

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
                HttpContext.Response.StatusCode = 400;
                HttpContext.Items.Add("errMsg", "通过指定的文件ID未找到任何文件");

                return null;
            }
        }



        /// <summary>
        /// 通过文件ID获取文件静态访问路径
        /// </summary>
        /// <param name="fileId">文件ID</param>
        /// <returns></returns>
        [HttpGet("GetFilePath")]
        public string? GetFilePath(long fileId)
        {

            var file = db.TFile.AsNoTracking().Where(t => t.IsDelete == false && t.Id == fileId).FirstOrDefault();

            if (file != null)
            {
                string fileServerUrl = configuration["FileServerUrl"]?.ToString() ?? "";

                string fileUrl = fileServerUrl + file.Path;

                return fileUrl;
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
                HttpContext.Items.Add("errMsg", "通过指定的文件ID未找到任何文件");

                return null;
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