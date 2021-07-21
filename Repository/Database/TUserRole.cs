using Repository.Database.Bases;
using System;

namespace Repository.Database
{
    public class TUserRole : CUD_User
    {


        /// <summary>
        /// 角色信息
        /// </summary>
        public Guid RoleId { get; set; }
        public virtual TRole Role { get; set; }



        /// <summary>
        /// 用户信息
        /// </summary>
        public Guid UserId { get; set; }
        public virtual TUser User { get; set; }


    }
}
