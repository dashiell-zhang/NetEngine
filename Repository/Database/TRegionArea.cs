using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.Database
{
    /// <summary>
    /// 区域信息表
    /// </summary>
    public  class TRegionArea
    {

        [DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        /// <summary>
        /// 区域名称
        /// </summary>
        public string Area { get; set; }


        /// <summary>
        /// 所属城市ID
        /// </summary>
        public int CityId { get; set; }
        public virtual TRegionCity City { get; set; }


    }
}
