﻿using Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Repository.Database;
using SkiaSharp;
using Web.Interface;
using WebAPIBasic.Models.Shared;

namespace WebAPI.Controllers
{

    /// <summary>
    /// 文件上传控制器
    /// </summary>
    [Authorize]
    [Route("[controller]/[action]")]
    [ApiController]
    public class FileController(IFileService fileService) : ControllerBase
    {



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
        public long RemoteUploadFile([FromQuery] string business, [FromQuery] long? key, [FromQuery] string sign, [FromQuery] bool isPublicRead, [FromBody] DtoKeyValue fileInfo)
        {
            return fileService.RemoteUploadFile(business, key, sign, isPublicRead, fileInfo);
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
        public long UploadFile([FromQuery] string business, [FromQuery] long? key, [FromQuery] string sign, bool isPublicRead, IFormFile file)
        {
            return fileService.UploadFile(business, key, sign, isPublicRead, file);
        }




        /// <summary>
        /// 通过文件ID获取文件
        /// </summary>
        /// <param name="webHostEnvironment"></param>
        /// <param name="db"></param>
        /// <param name="fileid">文件ID</param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpGet]
        public FileResult? GetFile([FromServices] IWebHostEnvironment webHostEnvironment, [FromServices] DatabaseContext db, long fileid)
        {
            string rootPath = webHostEnvironment.ContentRootPath;

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
        /// <param name="webHostEnvironment"></param>
        /// <param name="db"></param>
        /// <param name="fileId">图片ID</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <returns></returns>
        /// <remarks>不指定宽高参数,返回原图</remarks>
        [AllowAnonymous]
        [HttpGet]
        public FileResult? GetImage([FromServices] IWebHostEnvironment webHostEnvironment, [FromServices] DatabaseContext db, long fileId, int width, int height)
        {

            string rootPath = webHostEnvironment.ContentRootPath;

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
            return fileService.GetFileURL(fileId);
        }



        /// <summary>
        /// 通过文件ID删除文件方法
        /// </summary>
        /// <param name="id">文件ID</param>
        /// <returns></returns>
        [HttpDelete]
        public bool DeleteFile(long id)
        {
            return fileService.DeleteFile(id);
        }


    }
}