namespace WebApi.Models.v1.Sign
{

    /// <summary>
    /// 创建标记方法入参
    /// </summary>
    public class DtoSign
    {


        /// <summary>
        /// 业务领域
        /// </summary>
        public string Business { get; set; }


        /// <summary>
        /// 记录值
        /// </summary>
        public long Key { get; set; }


        /// <summary>
        /// 自定义标记
        /// </summary>
        public string Sign { get; set; }
    }
}
