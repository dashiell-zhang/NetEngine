namespace WebApi.Libraries.WeiXin.App.Models
{

    /// <summary>
    /// APP微信支付统一下单接口返回类型
    /// </summary>
    public class DtoCreatePayApp
    {

        public DtoCreatePayApp()
        {
            TimeSpan cha = DateTime.UtcNow - (new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            long t = (long)cha.TotalSeconds;
            TimeStamp = t.ToString();
        }


        /// <summary>
        /// 应用ID
        /// </summary>
        public string? AppId { get; set; }



        /// <summary>
        /// 商户号
        /// </summary>
        public string? PartnerId { get; set; }



        /// <summary>
        /// 预支付交易会话ID
        /// </summary>
        public string? PrepayId { get; set; }



        /// <summary>
        /// 扩展字段
        /// </summary>
        public string? Package { get; set; }


        /// <summary>
        /// 随机字符串，长度为32个字符以下
        /// </summary>
        public string? NonceStr { get; set; }



        /// <summary>
        /// 时间戳，从 1970 年 1 月 1 日 00:00:00 至今的秒数，即当前的时间
        /// </summary>
        public string TimeStamp { get; set; }



        /// <summary>
        /// 签名
        /// </summary>
        public string? Sign { get; set; }

    }
}
