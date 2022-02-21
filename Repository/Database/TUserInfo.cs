using Repository.Bases;
using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.Database
{
    /// <summary>
    /// 用户详细信息表
    /// </summary>
    public class TUserInfo : CUD
    {



        /// <summary>
        /// 用户ID
        /// </summary>
        public long UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual TUser User { get; set; }


        /// <summary>
        /// 地址区域ID
        /// </summary>
        public int? RegionAreaId { get; set; }
        public virtual TRegionArea? RegionArea { get; set; }



        /// <summary>
        /// 地址详细信息
        /// </summary>
        public string? Address { get; set; }



        /// <summary>
        /// 性别
        /// </summary>
        public bool? Sex { get; set; }



        /// <summary>
        /// 编辑人ID
        /// </summary>
        public long? UpdateUserId { get; set; }

        [ForeignKey("UpdateUserId")]
        public virtual TUser? UpdateUser { get; set; }


    }
}
