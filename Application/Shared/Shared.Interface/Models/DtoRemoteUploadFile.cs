namespace Shared.Interface.Models
{
    public class DtoRemoteUploadFile
    {

        /// <summary>
        /// 业务领域
        /// </summary>
        public string Business { get; set; }


        /// <summary>
        /// 关联记录值
        /// </summary>
        public long? Key { get; set; }


        /// <summary>
        /// 自定义标记
        /// </summary>
        public string Sign { get; set; }


        /// <summary>
        /// 是否允许公开访问
        /// </summary>
        public bool IsPublicRead { get; set; }


        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; }


        /// <summary>
        /// 文件流
        /// </summary>
        public string FileUrl { get; set; }


    }
}
