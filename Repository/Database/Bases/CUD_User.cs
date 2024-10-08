using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.Database.Bases
{


    /// <summary>
    /// 创建，编辑，删除，并关联了用户
    /// </summary>
    public class CUD_User : CD_User
    {

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTimeOffset? UpdateTime { get; set; }



        /// <summary>
        /// 编辑人ID
        /// </summary>
        public long? UpdateUserId { get; set; }

        [ForeignKey("UpdateUserId")]
        public virtual TUser? UpdateUser { get; set; }


    }
}
