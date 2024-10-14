using Client.Interface;
using Common;
using FileStorage;
using IdentifierGenerator;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using WebAPI.Core.Models.Shared;

namespace Client.Service
{
    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class FileService(DatabaseContext db, IUserContext userContext, IConfiguration configuration, IdService idService, IWebHostEnvironment webHostEnvironment, IFileStorage? fileStorage = null) : IFileService
    {
        private readonly string rootPath = webHostEnvironment.ContentRootPath;

        private long userId => userContext.UserId;




        public long RemoteUploadFile(string business, long? key, string sign, bool isPublicRead, DtoKeyValue fileInfo)
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
                    if (key == default(long))
                    {
                        key = null;
                    }

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



        public long UploadFile(string business, long? key, string sign, bool isPublicRead, IFormFile file)
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

                using (FileStream fs = File.Create(filePath))
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
                    if (key == default(long))
                    {
                        key = null;
                    }

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
