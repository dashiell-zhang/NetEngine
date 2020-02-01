using Repository.WebCore.Bases;

namespace Repository.WebCore
{


    /// <summary>
    /// 产品表
    /// </summary>
    public class TProduct : CUD_User
    {

        /// <summary>
        /// 编号
        /// </summary>
        public string Number { get; set; }


        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }


        /// <summary>
        /// 价格
        /// </summary>
        public decimal Price { get; set; }


        /// <summary>
        /// 描述
        /// </summary>
        public string Detail { get; set; }

    }
}
