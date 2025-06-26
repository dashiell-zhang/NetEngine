using Repository.Bases;

namespace Repository.Database
{
    /// <summary>
    /// 订单详情表
    /// </summary>
    public class TOrderDetail : CD
    {


        /// <summary>
        /// 订单ID
        /// </summary>
        public long OrderId { get; set; }
        public virtual TOrder Order { get; set; }


        /// <summary>
        /// 产品ID
        /// </summary>
        public long ProductId { get; set; }
        public virtual TProduct Product { get; set; }


        /// <summary>
        /// 产品数量
        /// </summary>
        public int Number { get; set; }

    }
}
