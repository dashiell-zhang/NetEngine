using Shared.Interface.Models;

namespace Shared.Interface
{
    public interface IFileService
    {
        /// <summary>
        /// 文件上传
        /// </summary>
        /// <param name="savePath">文件存储基础路径</param>
        /// <param name="uploadFile"></param>
        /// <returns></returns>
        public long UploadFile(string savePath, DtoUploadFile uploadFile);



        /// <summary>
        /// 远程单文件上传接口
        /// </summary>
        /// <param name="savePath">文件存储基础路径</param>
        /// <param name="remoteUploadFile"></param>
        /// <returns>文件ID</returns>
        public long RemoteUploadFile(string savePath, DtoRemoteUploadFile remoteUploadFile);



        /// <summary>
        /// 通过文件ID获取文件静态访问路径
        /// </summary>
        /// <param name="fileId">文件ID</param>
        /// <returns></returns>
        public string? GetFileURL(long fileId);



        /// <summary>
        /// 通过文件ID删除文件方法
        /// </summary>
        /// <param name="id">文件ID</param>
        /// <returns></returns>
        public bool DeleteFile(long id);

    }
}
