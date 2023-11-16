using Microsoft.EntityFrameworkCore;
using Repository.Database.Bases;

namespace Repository.Database
{


    /// <summary>
    /// 产品表
    /// </summary>
    [Index(nameof(SKU)), Index(nameof(Name))]
    public class TProduct : CUD_User
    {


        /// <summary>
        /// SKU
        /// </summary>
        public string SKU { get; set; }


        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }


        /// <summary>
        /// 价格
        /// </summary>
        [Precision(10, 2)]
        public decimal Price { get; set; }


        /// <summary>
        /// 描述
        /// </summary>
        public string Detail { get; set; }

    }
}
