using Common;
using FileStorage;
using IdentifierGenerator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Repository.Database;
using SkiaSharp;
using WebAPIBasic.Libraries;
using WebAPIBasic.Models.Shared;

namespace WebAPI.Controllers
{

    /// <summary>
    /// 文件上传控制器
    /// </summary>
    [Authorize]
    [Route("[controller]/[action]")]
    [ApiController]
    public class FileController : ControllerBase
    {


        private readonly DatabaseContext db;
        private readonly IConfiguration configuration;
        private readonly IdService idService;
        private readonly IFileStorage? fileStorage;

        private readonly string rootPath;

        private readonly long userId;



        public FileController(DatabaseContext db, IConfiguration configuration, IdService idService, IWebHostEnvironment webHostEnvironment, IHttpContextAccessor httpContextAccessor, IFileStorage? fileStorage = null)
        {
            this.db = db;
            this.configuration = configuration;
            this.idService = idService;
            this.fileStorage = fileStorage;

            rootPath = webHostEnvironment.ContentRootPath;

            var userIdStr = httpContextAccessor.HttpContext?.GetClaimByUser("userId");

            if (userIdStr != null)
            {
                userId = long.Parse(userIdStr);
            }
        }




        /// <summary>
        /// 远程单文件上传接口
        /// </summary>
        /// <param name="business">业务领域</param>
        /// <param name="key">记录值</param>
        /// <param name="sign">自定义标记</param>
        /// <param name="isPublicRead">是否允许公开访问</param>
        /// <param name="fileInfo">Key为文件URL,Value为文件名称</param>
        /// <returns>文件ID</returns>
        [HttpPost]
        public long RemoteUploadFile([FromQuery] string business, [FromQuery] long key, [FromQuery] string sign, [FromQuery] bool isPublicRead, [FromBody] DtoKeyValue fileInfo)
        {

            string remoteFileUrl = fileInfo.Key!.ToString()!;

            var fileExtension = Path.GetExtension(fileInfo.Value!.ToString()!).ToLower();
            var fileName = idService.GetId() + fileExtension;

            var utcNow = DateTime.UtcNow;

            string basePath = Path.Combine("files", utcNow.ToString("yyyy"), utcNow.ToString("MM"), utcNow.ToString("dd"));

            var folderPath = Path.Combine(rootPath, basePath);

            var dlPath = IOHelper.DownloadFile(remoteFileUrl, folderPath, fileName);

            if (dlPath != null)
            {

                var length = new FileInfo(dlPath).Length;

                var isSuccess = true;

                string fileInfoName = fileInfo.Value.ToString()!;

                if (fileStorage != null)
                {
                    var upload = fileStorage.FileUpload(dlPath, basePath, isPublicRead, fileInfoName);

                    if (upload)
                    {
                        IOHelper.DeleteFile(dlPath);
                    }
                    else
                    {
                        isSuccess = false;
                    }
                }

                if (isSuccess)
                {

                    var filePath = Path.Combine(basePath, fileName).Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    TFile f = new()
                    {
                        Id = idService.GetId(),
                        Name = fileInfoName,
                        Length = length,
                        IsPublicRead = isPublicRead,
                        Path = filePath,
                        Table = business,
                        TableId = key,
                        Sign = sign,
                        CreateUserId = userId
                    };
                    db.TFile.Add(f);
                    db.SaveChanges();

                    return f.Id;
                }

            }

            throw new CustomException("文件上传失败");
        }



        /// <summary>
        /// 单文件上传接口
        /// </summary>
        /// <param name="business">业务领域</param>
        /// <param name="key">记录值</param>
        /// <param name="sign">自定义标记</param>
        /// <param name="isPublicRead"></param>
        /// <param name="file">file</param>
        /// <returns>文件ID</returns>
        [DisableRequestSizeLimit]
        [HttpPost]
        public long UploadFile([FromQuery] string business, [FromQuery] long key, [FromQuery] string sign, bool isPublicRead, IFormFile file)
        {
            var utcNow = DateTime.UtcNow;

            string basePath = Path.Combine("files", utcNow.ToString("yyyy"), utcNow.ToString("MM"), utcNow.ToString("dd"));

            string folderPath = Path.Combine(rootPath, basePath);

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var fileName = idService.GetId() + Path.GetExtension(file.FileName).ToLower();

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
                    isSuccess = fileStorage.FileUpload(filePath, basePath, isPublicRead, file.FileName);

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
                        Id = idService.GetId(),
                        Name = file.FileName,
                        Length = file.Length,
                        IsPublicRead = isPublicRead,
                        Path = filePath,
                        Table = business,
                        TableId = key,
                        Sign = sign,
                        CreateUserId = userId
                    };
                    db.TFile.Add(f);
                    db.SaveChanges();

                    return f.Id;
                }

            }


            throw new CustomException("文件上传失败");
        }




        /// <summary>
        /// 通过文件ID获取文件
        /// </summary>
        /// <param name="fileid">文件ID</param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpGet]
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
                throw new CustomException("通过指定的文件ID未找到任何文件");
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
        [HttpGet]
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
                throw new CustomException("通过指定的文件ID未找到任何文件");
            }
        }



        /// <summary>
        /// 通过文件ID获取文件静态访问路径
        /// </summary>
        /// <param name="fileId">文件ID</param>
        /// <returns></returns>
        [HttpGet]
        public string? GetFileURL(long fileId)
        {

            var file = db.TFile.AsNoTracking().Where(t => t.Id == fileId).Select(t => new { t.Path, t.IsPublicRead }).FirstOrDefault();

            if (file != null)
            {
                string fileURL = "";

                if (file.IsPublicRead)
                {
                    string fileServerUrl = configuration["FileServerURL"]?.ToString() ?? "";
                    fileURL = fileServerUrl + file.Path;
                }
                else
                {
                    var tempURL = fileStorage?.GetFileTempURL(file.Path, TimeSpan.FromMinutes(10));

                    if (tempURL != null)
                    {
                        fileURL = tempURL;
                    }
                    else
                    {
                        throw new CustomException("文件临时授权地址获取失败");
                    }
                }

                return fileURL;

            }
            else
            {
                throw new CustomException("通过指定的文件ID未找到任何文件");
            }
        }



        /// <summary>
        /// 通过文件ID删除文件方法
        /// </summary>
        /// <param name="id">文件ID</param>
        /// <returns></returns>
        [HttpDelete]
        public bool DeleteFile(long id)
        {
            var file = db.TFile.Where(t => t.Id == id).FirstOrDefault();

            if (file != null)
            {
                file.IsDelete = true;
                file.DeleteUserId = userId;

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