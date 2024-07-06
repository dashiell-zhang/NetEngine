using Microsoft.EntityFrameworkCore;
using Repository.Database.Bases;
using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.Database
{

    /// <summary>
    /// 用户表
    /// </summary>
    [Index(nameof(UserName))]
    [Index(nameof(Phone))]
    [Index(nameof(Email))]
    public class TUser : CUD_User
    {


        /// <summary>
        /// 名称
        /// </summary>
        [Column(TypeName = "citext")]
        public string Name { get; set; }


        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; set; }


        /// <summary>
        /// 手机号
        /// </summary>
        public string Phone { get; set; }


        /// <summary>
        /// 邮箱
        /// </summary>
        public string? Email { get; set; }


        /// <summary>
        /// 密码
        /// </summary>
        public string Password { get; set; }


        /// <summary>
        /// 用户信息
        /// </summary>
        [InverseProperty("User")]
        public virtual TUserInfo? UserInfo { get; set; }


        [InverseProperty("CreateUser")]
        public virtual List<TUser>? InverseCreateUserList { get; set; }


        [InverseProperty("DeleteUser")]
        public virtual List<TUser>? InverseDeleteUserList { get; set; }


        [InverseProperty("UpdateUser")]
        public virtual List<TUser>? InverseUpdateUserList { get; set; }

    }
}
