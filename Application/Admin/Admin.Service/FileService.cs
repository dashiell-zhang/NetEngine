using Admin.Interface;
using Common;
using FileStorage;
using IdentifierGenerator;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;

namespace Admin.Service
{
    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class FileService(IUserContext userContext, DatabaseContext db, IConfiguration configuration, IdService idService, IWebHostEnvironment webHostEnvironment, IFileStorage? fileStorage = null) : IFileService
    {


        private readonly string rootPath = webHostEnvironment.WebRootPath;

        private long userId => userContext.UserId;




        public long UploadFile([FromQuery] string business, [FromQuery] long? key, [FromQuery] string sign, bool isPublicRead, IFormFile file)
        {
            var utcNow = DateTime.UtcNow;

            string basePath = Path.Combine("uploads", utcNow.ToString("yyyy"), utcNow.ToString("MM"), utcNow.ToString("dd"));

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



        public string? GetFilePath(long fileId)
        {

            var file = db.TFile.AsNoTracking().Where(t => t.Id == fileId).FirstOrDefault();

            if (file != null)
            {
                string fileServerUrl = configuration["FileServerUrl"]?.ToString() ?? "";

                string fileUrl = fileServerUrl + file.Path;

                return fileUrl;
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
