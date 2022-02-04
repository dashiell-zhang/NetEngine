using Repository.Bases;
using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.Database.Bases
{

    /// <summary>
    /// 创建，编辑，删除，并关联了用户
    /// </summary>
    public class CD_User : CD
    {

        /// <summary>
        /// 创建人ID
        /// </summary>
        public long? CreateUserId { get; set; }

        [ForeignKey("CreateUserId")]
        public virtual TUser? CreateUser { get; set; }



        /// <summary>
        /// 删除人ID
        /// </summary>
        public long? DeleteUserId { get; set; }

        [ForeignKey("DeleteUserId")]
        public virtual TUser? DeleteUser { get; set; }
    }
}
