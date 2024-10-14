using Microsoft.AspNetCore.Http;

namespace Admin.Interface
{
    public interface IFileService
    {

        /// <summary>
        /// 单文件上传接口
        /// </summary>
        /// <param name="business">业务领域</param>
        /// <param name="key">记录值</param>
        /// <param name="sign">自定义标记</param>
        /// <param name="isPublicRead">是否允许公开访问</param>
        /// <param name="file">file</param>
        /// <returns>文件ID</returns>
        public long UploadFile( string business,  long? key,string sign, bool isPublicRead, IFormFile file);



        /// <summary>
        /// 通过文件ID获取文件静态访问路径
        /// </summary>
        /// <param name="fileId">文件ID</param>
        /// <returns></returns>
        public string? GetFilePath(long fileId);



        /// <summary>
        /// 通过文件ID删除文件方法
        /// </summary>
        /// <param name="id">文件ID</param>
        /// <returns></returns>
        public bool DeleteFile(long id);
    }
}
