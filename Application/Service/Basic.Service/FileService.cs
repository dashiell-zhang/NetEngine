using Authorize.Interface;
using Basic.Interface;
using Basic.Model.File;
using Common;
using FileStorage;
using IdentifierGenerator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;

namespace Basic.Service
{
    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class FileService(IdService idService, IUserContext userContext, DatabaseContext db, IConfiguration configuration, IFileStorage? fileStorage = null) : IFileService
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

            File.Move(uploadFile.TempFilePath, filePath);

            FileInfo fileInfo = new(filePath);

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
                    Length = fileInfo.Length,
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

            var tempDirPath = Path.Combine(savePath, "temps");

            if (!Directory.Exists(tempDirPath))
            {
                Directory.CreateDirectory(tempDirPath);
            }

            var tempFileName = Guid.NewGuid().ToString() + Path.GetExtension(remoteUploadFile.FileName);

            var tempFilePath = IOHelper.DownloadFile(remoteUploadFile.FileUrl, tempDirPath, tempFileName);

            if (tempFilePath != null)
            {
                DtoUploadFile uploadFile = new()
                {
                    Business = remoteUploadFile.Business,
                    Key = remoteUploadFile.Key,
                    Sign = remoteUploadFile.Sign,
                    IsPublicRead = remoteUploadFile.IsPublicRead,
                    FileName = remoteUploadFile.FileName,
                    TempFilePath = tempFilePath
                };

                return UploadFile(savePath, uploadFile);
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
