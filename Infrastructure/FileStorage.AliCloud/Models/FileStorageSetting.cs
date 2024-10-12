namespace FileStorage.AliCloud.Models
{
    public class FileStorageSetting
    {

        /// <summary>
        /// 对象存储区域节点
        /// </summary>
        public string Endpoint { get; set; }


        /// <summary>
        /// 账户ID
        /// </summary>
        public string AccessKeyId { get; set; }


        /// <summary>
        /// 账户私钥
        /// </summary>
        public string AccessKeySecret { get; set; }


        /// <summary>
        /// 存储桶名称
        /// </summary>
        public string BucketName { get; set; }


        /// <summary>
        /// 访问URL
        /// </summary>
        public string URL { get; set; }
    }



}
