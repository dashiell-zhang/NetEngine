using Microsoft.EntityFrameworkCore;
using Repository.Database.Bases;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.Database
{

    /// <summary>
    /// 用户表
    /// </summary>
    [Index(nameof(Name), nameof(PassWord))]
    [Index(nameof(Phone), nameof(PassWord))]
    [Index(nameof(Email), nameof(PassWord))]
    public class TUser : CUD_User
    {



        /// <summary>
        /// 用户名
        /// </summary>
        [DisplayName("用户名")]
        public string Name { get; set; }


        /// <summary>
        /// 昵称
        /// </summary>
        public string NickName { get; set; }


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
        public string PassWord { get; set; }



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
