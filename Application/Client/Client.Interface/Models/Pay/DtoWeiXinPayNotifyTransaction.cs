namespace Client.Interface.Models.Pay
{
#pragma warning disable IDE1006 // 命名样式

    /// <summary>
    /// 微信支付异步通知密文解码后的资源模型
    /// </summary>
    public class DtoWeiXinPayNotifyTransaction
    {
        public string mchid { get; set; }
        public string appid { get; set; }
        public string out_trade_no { get; set; }
        public string transaction_id { get; set; }
        public string trade_type { get; set; }
        public string trade_state { get; set; }
        public string trade_state_desc { get; set; }
        public string bank_type { get; set; }
        public string attach { get; set; }
        public DateTimeOffset success_time { get; set; }
        public Payer payer { get; set; }

        public class Payer
        {
            public string openid { get; set; }
        }

        public Amount amount { get; set; }

        public class Amount
        {
            public int total { get; set; }
            public int payer_total { get; set; }
            public string currency { get; set; }
            public string payer_currency { get; set; }
        }

    }

}
