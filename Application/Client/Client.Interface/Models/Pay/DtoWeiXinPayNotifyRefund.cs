namespace Client.Interface.Models.Pay
{
#pragma warning disable IDE1006 // 命名样式

    public class DtoWeiXinPayNotifyRefund
    {
        public string mchid { get; set; }
        public string out_trade_no { get; set; }
        public string transaction_id { get; set; }
        public string out_refund_no { get; set; }
        public string refund_id { get; set; }
        public string refund_status { get; set; }
        public DateTime success_time { get; set; }
        public Amount amount { get; set; }

        public class Amount
        {
            public int total { get; set; }
            public int refund { get; set; }
            public int payer_total { get; set; }
            public int payer_refund { get; set; }
        }

        public string user_received_account { get; set; }
    }


}
