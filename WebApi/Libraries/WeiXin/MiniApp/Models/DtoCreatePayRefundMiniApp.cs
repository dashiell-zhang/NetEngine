namespace WebApi.Libraries.WeiXin.MiniApp.Models
{


    /// <summary>
    /// 小程序微信支付统一下单接口返回类型
    /// </summary>
    public class DtoCreatePayRefundMiniApp
    {

        /// <summary>
        /// 返回状态码,SUCCESS/FAIL
        /// </summary>
        public string Return_code { get; set; }


        /// <summary>
        /// 返回信息
        /// </summary>
        public string Return_msg { get; set; }




        /// <summary>
        /// 业务结果，SUCCESS/FAIL,SUCCESS退款申请接收成功，结果通过退款查询接口查询,FAIL 提交业务失败
        /// </summary>
        public string Result_code { get; set; }



        /// <summary>
        /// 错误代码
        /// </summary>
        public string Err_code { get; set; }



        /// <summary>
        /// 错误代码描述
        /// </summary>
        public string Err_code_des { get; set; }



        /// <summary>
        /// 小程序ID
        /// </summary>
        public string AppId { get; set; }



        /// <summary>
        /// 商户号
        /// </summary>
        public string Mch_id { get; set; }



        /// <summary>
        /// 随机字符串
        /// </summary>
        public string Nonce_str { get; set; }



        /// <summary>
        /// 签名
        /// </summary>
        public string Sign { get; set; }



        /// <summary>
        /// 微信支付订单号
        /// </summary>
        public string Transaction_Id { get; set; }



        /// <summary>
        /// 商户订单号
        /// </summary>
        public string Out_trade_no { get; set; }



        /// <summary>
        /// 商户退款单号
        /// </summary>
        public string Out_refund_no { get; set; }



        /// <summary>
        /// 微信退款单号
        /// </summary>
        public string Refund_id { get; set; }




        /// <summary>
        /// 退款金额,退款总金额,单位为分,可以做部分退款
        /// </summary>
        public int Refund_fee { get; set; }




        /// <summary>
        /// 订单总金额，单位为分，只能为整数
        /// </summary>
        public int Total_fee { get; set; }




        /// <summary>
        /// 现金支付金额，单位为分，只能为整数
        /// </summary>
        public int Cash_fee { get; set; }




        /// <summary>
        /// 现金退款金额，单位为分，只能为整数
        /// </summary>
        public int Cash_refund_fee { get; set; }


    }
}
