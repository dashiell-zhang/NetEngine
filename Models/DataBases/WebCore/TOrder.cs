using Models.DataBases.WebCore.Bases;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models.DataBases.WebCore
{


    /// <summary>
    /// 订单表
    /// </summary>
    public class TOrder : CUD_User
    {


        /// <summary>
        /// 订单号
        /// </summary>
        public string OrderNo { get; set; }


        /// <summary>
        /// 产品ID
        /// </summary>
        public string ProductId { get; set; }
        public TProduct Product { get; set; }


        /// <summary>
        /// 订单类型
        /// </summary>
        public string Type { get; set; }


        /// <summary>
        /// 价格
        /// </summary>
        public decimal Price { get; set; }


        /// <summary>
        /// 支付流水号
        /// </summary>
        public string SerialNo { get; set; }


        /// <summary>
        /// 订单状态
        /// </summary>
        public string State { get; set; }


        /// <summary>
        /// 支付方式
        /// </summary>
        public string PayType { get; set; }


        /// <summary>
        /// 支付状态
        /// </summary>
        [Column(TypeName = "bit")]
        public bool Paystate { get; set; }


        /// <summary>
        /// 支付时间
        /// </summary>
        public DateTime? Paytime { get; set; }



        /// <summary>
        /// 实际支付金额
        /// </summary>
        public decimal? Payprice { get; set; }


    }
}
