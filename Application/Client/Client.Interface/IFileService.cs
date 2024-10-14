using Microsoft.AspNetCore.Http;
using WebAPI.Core.Models.Shared;

namespace Client.Interface
{
    public interface IFileService
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
        public long RemoteUploadFile(string business, long? key, string sign, bool isPublicRead, DtoKeyValue fileInfo);



        /// <summary>
        /// 单文件上传接口
        /// </summary>
        /// <param name="business">业务领域</param>
        /// <param name="key">记录值</param>
        /// <param name="sign">自定义标记</param>
        /// <param name="isPublicRead"></param>
        /// <param name="file">file</param>
        /// <returns>文件ID</returns>
        public long UploadFile(string business, long? key, string sign, bool isPublicRead, IFormFile file);





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
