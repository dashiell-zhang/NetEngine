using Microsoft.EntityFrameworkCore;
using Repository.Bases;
using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.Database
{
    /// <summary>
    /// 区域信息表
    /// </summary>
    [Index(nameof(Area))]
    public class TRegionArea : CD
    {



        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public new int Id { get; set; }



        /// <summary>
        /// 区域名称
        /// </summary>
        public string Area { get; set; }



        /// <summary>
        /// 所属城市ID
        /// </summary>
        public int CityId { get; set; }
        public virtual TRegionCity City { get; set; }



        /// <summary>
        /// 城市下所有乡镇信息
        /// </summary>
        public virtual List<TRegionTown>? TRegionTown { get; set; }


    }
}
