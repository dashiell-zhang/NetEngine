namespace Client.Interface.Models.Pay
{
    public class DtoCreateWeiXinPayAppRet
    {


        /// <summary>
        /// 应用Id
        /// </summary>
        public string AppId { get; set; }


        /// <summary>
        /// 商户号
        /// </summary>
        public string PartnerId { get; set; }



        /// <summary>
        /// 预支付交易会话ID
        /// </summary>
        public string PrepayId { get; set; }



        /// <summary>
        /// 随机字符串，长度为32个字符以下
        /// </summary>
        public string NonceStr { get; set; }



        /// <summary>
        /// 时间戳，从 1970 年 1 月 1 日 00:00:00 至今的秒数，即当前的时间
        /// </summary>
        public long TimeStamp { get; set; }



        /// <summary>
        /// 签名
        /// </summary>
        public string Sign { get; set; }
    }
}
