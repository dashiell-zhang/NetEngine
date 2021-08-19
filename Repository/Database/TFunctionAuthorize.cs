using Repository.Database.Bases;
using System;

namespace Repository.Database
{

    /// <summary>
    /// 功能授权配置表
    /// </summary>
    public class TFunctionAuthorize : CUD_User
    {


        /// <summary>
        /// 功能ID
        /// </summary>
        public Guid FunctionId { get; set; }
        public virtual TFunction Function { get; set; }



        /// <summary>
        /// 角色ID
        /// </summary>
        public Guid? RoleId { get; set; }
        public virtual TRole Role { get; set; }




        /// <summary>
        /// 用户信息
        /// </summary>
        public Guid? UserId { get; set; }
        public virtual TUser User { get; set; }


    }
}
