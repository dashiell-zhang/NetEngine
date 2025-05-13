using Application.Core.Interfaces.Authorize;
using Application.Core.Interfaces.Basic;
using Application.Model.Basic.File;
using Common;
using FileStorage;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;

namespace Application.Core.Services.Basic
{
    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class FileService(IdService idService, IUserContext userContext, DatabaseContext db, IConfiguration configuration, IFileStorage? fileStorage = null) : IFileService
    {


        public async Task<long> UploadFileAsync(string savePath, DtoUploadFile uploadFile)
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

            long fileLength = new FileInfo(filePath).Length;

            if (fileStorage != null)
            {
                isSuccess = await fileStorage.FileUploadAsync(filePath, basePath, uploadFile.IsPublicRead, uploadFile.FileName);

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
                    Length = fileLength,
                    IsPublicRead = uploadFile.IsPublicRead,
                    Path = filePath,
                    Table = uploadFile.Business,
                    TableId = uploadFile.Key,
                    Sign = uploadFile.Sign,
                    CreateUserId = userContext.UserId
                };

                db.TFile.Add(f);
                await db.SaveChangesAsync();

                return f.Id;
            }


            throw new CustomException("文件上传失败");

        }



        public async Task<long> RemoteUploadFileAsync(string savePath, DtoRemoteUploadFile remoteUploadFile)
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

                return await UploadFileAsync(savePath, uploadFile);
            }

            throw new CustomException("文件上传失败");
        }




        public async Task<string?> GetFileUrlAsync(long fileId, bool isInline = false)
        {
            var file = await db.TFile.Where(t => t.Id == fileId).Select(t => new { t.Path, t.IsPublicRead }).FirstOrDefaultAsync();

            if (file != null)
            {
                string fileUrl = "";

                if (file.IsPublicRead || fileStorage == null)
                {
                    string fileServerUrl = configuration["FileServerUrl"]?.ToString() ?? "";
                    fileUrl = fileServerUrl + file.Path;
                }
                else
                {
                    var tempUrl = fileStorage.GetFileUrl(file.Path, TimeSpan.FromMinutes(10), isInline);

                    if (tempUrl != null)
                    {
                        fileUrl = tempUrl;
                    }
                    else
                    {
                        throw new CustomException("文件临时授权地址获取失败");
                    }
                }

                return fileUrl;

            }
            else
            {
                throw new CustomException("通过指定的文件ID未找到任何文件");
            }
        }



        public async Task<bool> DeleteFileAsync(long id)
        {
            var file = await db.TFile.Where(t => t.Id == id).FirstOrDefaultAsync();

            if (file != null)
            {
                file.IsDelete = true;
                file.DeleteUserId = userContext.UserId;

                await db.SaveChangesAsync();

                return true;
            }
            else
            {
                return false;
            }
        }



        public async Task<List<DtoFileInfo>> GetFileListAsync(string business, string? sign, long key, bool isGetUrl)
        {

            var query = db.TFile.Where(t => t.Table == business && t.TableId == key);

            if (sign != null)
            {
                query = query.Where(t => t.Sign == sign);
            }

            var fileList = await query.OrderBy(t => t.Sort).ThenBy(t => t.CreateTime).Select(t => new DtoFileInfo
            {
                Id = t.Id,
                Name = t.Name,
                Length = t.Length,
                Sign = t.Sign,
                Path = t.Path,
            }).ToListAsync();

            foreach (var file in fileList)
            {
                file.LengthText = IOHelper.FileLengthToString(file.Length);

                if (isGetUrl)
                {
                    file.Url = await GetFileUrlAsync(file.Id);
                }
            }

            return fileList;
        }
    }
}
