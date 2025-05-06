using Application.Model.Basic.File;

namespace Application.Core.Interfaces.Basic
{
    public interface IFileService
    {
        /// <summary>
        /// 文件上传
        /// </summary>
        /// <param name="savePath">文件存储基础路径</param>
        /// <param name="uploadFile"></param>
        /// <returns></returns>
        Task<long> UploadFileAsync(string savePath, DtoUploadFile uploadFile);



        /// <summary>
        /// 远程单文件上传接口
        /// </summary>
        /// <param name="savePath">文件存储基础路径</param>
        /// <param name="remoteUploadFile"></param>
        /// <returns>文件ID</returns>
        Task<long> RemoteUploadFileAsync(string savePath, DtoRemoteUploadFile remoteUploadFile);



        /// <summary>
        /// 通过文件ID获取文件静态访问路径
        /// </summary>
        /// <param name="fileId">文件ID</param>
        /// <param name="isInline">是否在浏览器中打开</param>
        /// <returns></returns>
        Task<string?> GetFileUrlAsync(long fileId, bool isInline = false);



        /// <summary>
        /// 通过文件ID删除文件方法
        /// </summary>
        /// <param name="id">文件ID</param>
        /// <returns></returns>
        Task<bool> DeleteFileAsync(long id);



        /// <summary>
        /// 获取文件列表
        /// </summary>
        /// <param name="business">业务领域</param>
        /// <param name="sign">标记</param>
        /// <param name="key">关联记录值</param>
        /// <param name="isGetUrl">是否获取url</param>
        /// <returns></returns>
        Task<List<DtoFileInfo>> GetFileListAsync(string business, string? sign, long key, bool isGetUrl);

    }
}
