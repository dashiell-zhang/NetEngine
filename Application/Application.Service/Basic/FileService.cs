using Application.Interface;
using Application.Model.Basic.File;
using Common;
using FileStorage;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Repository;
using Repository.Database;
using SourceGenerator.Runtime.Attributes;

namespace Application.Service.Basic;

/// <summary>
/// 提供统一文件上传、访问、绑定和删除能力
/// </summary>
[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class FileService(IdService idService, IUserContext userContext, DatabaseContext db, IConfiguration configuration, IHttpClientFactory httpClientFactory, IFileStorage? fileStorage = null)
{

    /// <summary>
    /// 文件上传
    /// </summary>
    /// <param name="savePath">文件存储基础路径</param>
    /// <param name="uploadFile"></param>
    /// <returns></returns>
    public async Task<long> UploadFileAsync(string savePath, UploadFileDto uploadFile)
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

        System.IO.File.Move(uploadFile.TempFilePath, filePath);

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

            StoredFile f = new()
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

            db.StoredFile.Add(f);
            await db.SaveChangesAsync();

            return f.Id;
        }


        throw new CustomException("文件上传失败");

    }


    /// <summary>
    /// 远程单文件上传接口
    /// </summary>
    /// <param name="savePath">文件存储基础路径</param>
    /// <param name="remoteUploadFile"></param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文件ID</returns>
    public async Task<long> RemoteUploadFileAsync(string savePath, RemoteUploadFileDto remoteUploadFile, CancellationToken cancellationToken = default)
    {

        var tempDirPath = Path.Combine(savePath, "temps");

        if (!Directory.Exists(tempDirPath))
        {
            Directory.CreateDirectory(tempDirPath);
        }

        var tempFileName = Guid.NewGuid().ToString() + Path.GetExtension(remoteUploadFile.FileName);

        var httpClient = httpClientFactory.CreateClient();

        string tempFilePath;

        try
        {
            tempFilePath = await httpClient.DownloadFileAsync(remoteUploadFile.FileUrl, tempDirPath, tempFileName, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CustomException("远程文件下载失败", ex);
        }

        UploadFileDto uploadFile = new()
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


    /// <summary>
    /// 将上传批次文件绑定到正式业务记录
    /// </summary>
    /// <param name="business">业务领域</param>
    /// <param name="sign">文件标记</param>
    /// <param name="uploadKey">上传批次标识</param>
    /// <param name="businessId">正式业务记录标识</param>
    /// <returns>绑定文件数量</returns>
    public async Task<int> BindFilesAsync(string business, string sign, long uploadKey, long businessId)
    {

        var fileList = await db.StoredFile.Where(t => t.Table == business && t.Sign == sign && t.TableId == uploadKey && t.CreateUserId == userContext.UserId).ToListAsync();

        foreach (var file in fileList)
        {
            file.TableId = businessId;
        }

        return fileList.Count;

    }


    /// <summary>
    /// 根据正文实际引用同步上传批次和正式业务记录中的正文文件
    /// </summary>
    /// <param name="business">业务领域</param>
    /// <param name="uploadKey">上传批次标识</param>
    /// <param name="businessId">正式业务记录标识</param>
    /// <param name="referencedFileIds">正文实际引用的文件标识</param>
    /// <returns>保留的正文文件数量</returns>
    public async Task<int> SyncContentFilesAsync(string business, long uploadKey, long businessId, IReadOnlyCollection<long> referencedFileIds)
    {

        var distinctFileIds = referencedFileIds.Distinct().ToHashSet();
        var currentUserId = userContext.UserId;
        var fileList = await db.StoredFile.Where(t => t.Table == business && t.Sign.StartsWith("content-") && (t.TableId == businessId || (t.TableId == uploadKey && t.CreateUserId == currentUserId))).ToListAsync();
        var invalidFileIds = distinctFileIds.Except(fileList.Select(t => t.Id)).ToList();

        if (invalidFileIds.Count > 0)
        {
            throw new CustomException("正文包含无效或不属于当前文章的文件");
        }

        var deleteTime = DateTimeOffset.UtcNow;

        foreach (var file in fileList)
        {
            if (distinctFileIds.Contains(file.Id))
            {
                if (file.TableId == uploadKey)
                {
                    file.TableId = businessId;
                }
            }
            else
            {
                file.DeleteTime = deleteTime;
                file.DeleteUserId = currentUserId;
            }
        }

        return distinctFileIds.Count;

    }


    /// <summary>
    /// 软删除正式业务记录关联的全部文件
    /// </summary>
    /// <param name="business">业务领域</param>
    /// <param name="businessId">正式业务记录标识</param>
    /// <returns>软删除文件数量</returns>
    public async Task<int> SoftDeleteBusinessFilesAsync(string business, long businessId)
    {

        var fileList = await db.StoredFile.Where(t => t.Table == business && t.TableId == businessId).ToListAsync();
        var deleteTime = DateTimeOffset.UtcNow;

        foreach (var file in fileList)
        {
            file.DeleteTime = deleteTime;
            file.DeleteUserId = userContext.UserId;
        }

        return fileList.Count;

    }


    /// <summary>
    /// 通过文件ID获取文件静态访问路径
    /// </summary>
    /// <param name="fileId">文件ID</param>
    /// <param name="isInline">是否在浏览器中打开</param>
    /// <returns></returns>
    public async Task<string?> GetFileUrlAsync(long fileId, bool isInline = false)
    {
        var file = await db.StoredFile.Where(t => t.Id == fileId).Select(t => new { t.Path, t.IsPublicRead }).FirstOrDefaultAsync();

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


    /// <summary>
    /// 通过文件ID删除文件方法
    /// </summary>
    /// <param name="id">文件ID</param>
    /// <returns></returns>
    public async Task<bool> DeleteFileAsync(long id)
    {
        var file = await db.StoredFile.Where(t => t.Id == id).FirstOrDefaultAsync();

        if (file != null)
        {
            file.DeleteTime = DateTimeOffset.UtcNow;
            file.DeleteUserId = userContext.UserId;

            await db.SaveChangesAsync();

            return true;
        }
        else
        {
            return false;
        }
    }


    /// <summary>
    /// 获取文件列表
    /// </summary>
    /// <param name="business">业务领域</param>
    /// <param name="sign">标记</param>
    /// <param name="key">关联记录值</param>
    /// <param name="isGetUrl">是否获取url</param>
    /// <returns></returns>
    public async Task<List<FileInfoDto>> GetFileListAsync(string business, string? sign, long key, bool isGetUrl)
    {

        var query = db.StoredFile.Where(t => t.Table == business && t.TableId == key);

        if (sign != null)
        {
            query = query.Where(t => t.Sign == sign);
        }

        var fileList = await query.OrderBy(t => t.Sort).ThenBy(t => t.Id).Select(t => new FileInfoDto
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
