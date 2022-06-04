namespace WebApi.Models.AppSetting
{
    public class TencentCloudSMSSetting
    {


        /// <summary>
        /// SDK AppId (非账号APPId)
        /// </summary>
        public string AppId { get; set; }


        /// <summary>
        /// 账号密钥ID
        /// </summary>
        public string SecretId { get; set; }


        /// <summary>
        /// 账号密钥Key
        /// </summary>
        public string SecretKey { get; set; }
    }
}
