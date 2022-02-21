using Repository.Database.Bases;

namespace Repository.Database
{
    public class TUserRole : CUD_User
    {


        /// <summary>
        /// 角色信息
        /// </summary>
        public long RoleId { get; set; }
        public virtual TRole Role { get; set; }



        /// <summary>
        /// 用户信息
        /// </summary>
        public long UserId { get; set; }
        public virtual TUser User { get; set; }


    }
}
