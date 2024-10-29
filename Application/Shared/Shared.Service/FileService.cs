using Common;
using FileStorage;
using IdentifierGenerator;
using Microsoft.Extensions.Configuration;
using Repository.Database;
using Shared.Interface;
using Shared.Interface.Models;

namespace Shared.Service
{
    public class FileService(IdService idService, IUserContext userContext, DatabaseContext db, IFileStorage? fileStorage, IConfiguration configuration) : IFileService
    {


        public long UploadFile(string savePath, DtoUploadFile uploadFile)
        {
            var utcNow = DateTime.UtcNow;

            string basePath = Path.Combine("uploads", utcNow.ToString("yyyy"), utcNow.ToString("MM"), utcNow.ToString("dd"));

            string folderPath = Path.Combine(savePath, basePath);

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var fileName = idService.GetId() + Path.GetExtension(uploadFile.FileName).ToLower();

            var filePath = Path.Combine(folderPath, fileName);

            var isSuccess = true;

            using (FileStream fs = File.Create(filePath))
            {
                uploadFile.FileContent.CopyTo(fs);
                fs.Flush();
            }


            if (fileStorage != null)
            {
                isSuccess = fileStorage.FileUpload(filePath, basePath, uploadFile.IsPublicRead, uploadFile.FileName);

                if (isSuccess)
                {
                    IOHelper.DeleteFile(filePath);
                }
            }


            if (isSuccess)
            {
                if (uploadFile.Key == default(long))
                {
                    uploadFile.Key = null;
                }

                filePath = Path.Combine(basePath, fileName).Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                TFile f = new()
                {
                    Id = idService.GetId(),
                    Name = uploadFile.FileName,
                    Length = uploadFile.FileContent.Length,
                    IsPublicRead = uploadFile.IsPublicRead,
                    Path = filePath,
                    Table = uploadFile.Business,
                    TableId = uploadFile.Key,
                    Sign = uploadFile.Sign,
                    CreateUserId = userContext.UserId
                };

                db.TFile.Add(f);
                db.SaveChanges();

                return f.Id;
            }


            throw new CustomException("文件上传失败");

        }



        public long RemoteUploadFile(string savePath, DtoRemoteUploadFile remoteUploadFile)
        {

            var fileExtension = Path.GetExtension(remoteUploadFile.FileName).ToLower();
            var fileName = idService.GetId() + fileExtension;

            var utcNow = DateTime.UtcNow;

            string basePath = Path.Combine("files", utcNow.ToString("yyyy"), utcNow.ToString("MM"), utcNow.ToString("dd"));

            var folderPath = Path.Combine(savePath, basePath);

            var dlPath = IOHelper.DownloadFile(remoteUploadFile.FileUrl, folderPath, fileName);

            if (dlPath != null)
            {
                string filePath = dlPath;

                var isSuccess = true;

                long length = new FileInfo(filePath).Length;

                if (fileStorage != null)
                {
                    isSuccess = fileStorage.FileUpload(filePath, basePath, remoteUploadFile.IsPublicRead, fileName);

                    if (isSuccess)
                    {
                        IOHelper.DeleteFile(filePath);
                    }
                }

                if (isSuccess)
                {
                    if (remoteUploadFile.Key == default(long))
                    {
                        remoteUploadFile.Key = null;
                    }

                    filePath = Path.Combine(basePath, fileName).Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    TFile f = new()
                    {
                        Id = idService.GetId(),
                        Name = remoteUploadFile.FileName,
                        Length = length,
                        IsPublicRead = remoteUploadFile.IsPublicRead,
                        Path = filePath,
                        Table = remoteUploadFile.Business,
                        TableId = remoteUploadFile.Key,
                        Sign = remoteUploadFile.Sign,
                        CreateUserId = userContext.UserId
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
            var file = db.TFile.Where(t => t.Id == fileId).Select(t => new { t.Path, t.IsPublicRead }).FirstOrDefault();

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
                file.DeleteUserId = userContext.UserId;

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
