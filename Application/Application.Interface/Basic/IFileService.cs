using Application.Model.Basic.File;

namespace Application.Interface.Basic;

/// <summary>
/// 文件上传、访问与业务关联管理
/// </summary>
public interface IFileService
{

    /// <summary>
    /// 按文件ID获取文件元数据，未找到时返回空
    /// </summary>
    Task<FileInfoDto?> GetFileAsync(long fileId);


    /// <summary>
    /// 文件上传
    /// </summary>
    Task<long> UploadFileAsync(long actorUserId, string savePath, UploadFileDto uploadFile);


    /// <summary>
    /// 远程单文件上传接口
    /// </summary>
    Task<long> RemoteUploadFileAsync(long actorUserId, string savePath, RemoteUploadFileDto remoteUploadFile, CancellationToken cancellationToken = default);


    /// <summary>
    /// 将上传批次文件绑定到正式业务记录
    /// </summary>
    Task<int> BindFilesAsync(long actorUserId, string business, string sign, long uploadKey, long businessId);


    /// <summary>
    /// 根据正文实际引用同步上传批次和正式业务记录中的正文文件
    /// </summary>
    Task<int> SyncContentFilesAsync(long actorUserId, string business, long uploadKey, long businessId, IReadOnlyCollection<long> referencedFileIds);


    /// <summary>
    /// 软删除正式业务记录关联的全部文件
    /// </summary>
    Task<int> SoftDeleteBusinessFilesAsync(long actorUserId, string business, long businessId);


    /// <summary>
    /// 通过文件ID获取文件静态访问路径
    /// </summary>
    Task<string?> GetFileUrlAsync(long fileId, bool isInline = false);


    /// <summary>
    /// 通过文件ID删除文件方法
    /// </summary>
    Task<bool> DeleteFileAsync(long actorUserId, long id);


    /// <summary>
    /// 获取文件列表
    /// </summary>
    Task<List<FileInfoDto>> GetFileListAsync(string business, string? sign, long key, bool isGetUrl);

}
