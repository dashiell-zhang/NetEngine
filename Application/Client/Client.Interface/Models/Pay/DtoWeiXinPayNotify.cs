namespace Client.Interface.Models.Pay
{
#pragma warning disable IDE1006 // 命名样式

    /// <summary>
    /// 微信支付异步通知入参
    /// </summary>
    public class DtoWeiXinPayNotify
    {
        public string id { get; set; }
        public DateTimeOffset create_time { get; set; }
        public string resource_type { get; set; }
        public string event_type { get; set; }
        public string summary { get; set; }
        public Resource resource { get; set; }


        public class Resource
        {
            public string original_type { get; set; }
            public string algorithm { get; set; }
            public string ciphertext { get; set; }
            public string associated_data { get; set; }
            public string nonce { get; set; }
        }
    }



}
