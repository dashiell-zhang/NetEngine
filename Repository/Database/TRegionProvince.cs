using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.Database
{

    /// <summary>
    /// 省份信息表
    /// </summary>
    public partial class TRegionProvince
    {

        [DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.None)]
        public int Id { get; set; }


        /// <summary>
        /// 身份
        /// </summary>
        public string Province { get; set; }


        /// <summary>
        /// 省份下包含的所有城市信息
        /// </summary>
        public virtual ICollection<TRegionCity> TRegionCity { get; set; }
    }
}
