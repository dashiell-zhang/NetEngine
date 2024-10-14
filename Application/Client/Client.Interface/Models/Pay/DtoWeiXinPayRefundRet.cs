namespace Client.Interface.Models.Pay
{
#pragma warning disable IDE1006 // 命名样式
    /// <summary>
    /// 微信支付退款结果
    /// </summary>
    public class DtoWeiXinPayRefundRet
    {
        public Amount amount { get; set; }
        public string channel { get; set; }
        public DateTimeOffset create_time { get; set; }
        public string funds_account { get; set; }
        public string out_refund_no { get; set; }
        public string out_trade_no { get; set; }
        public string refund_id { get; set; }
        public string status { get; set; }
        public DateTimeOffset success_time { get; set; }
        public string transaction_id { get; set; }
        public string user_received_account { get; set; }


        public class Amount
        {
            public string currency { get; set; }
            public int discount_refund { get; set; }
            public int payer_refund { get; set; }
            public int payer_total { get; set; }
            public int refund { get; set; }
            public int refund_fee { get; set; }
            public int settlement_refund { get; set; }
            public int settlement_total { get; set; }
            public int total { get; set; }
        }

    }

}
