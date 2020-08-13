using Repository.Bases;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.Database
{

    /// <summary>
    /// 用户表
    /// </summary>
    public class TUser : CUD
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
        public string Email { get; set; }


        /// <summary>
        /// 密码
        /// </summary>
        public string PassWord { get; set; }


        /// <summary>
        /// 角色ID
        /// </summary>
        public Guid RoleId { get; set; }
        public TRole Role { get; set; }



        /// <summary>
        /// 该用户所有得订单信息
        /// </summary>
        [InverseProperty("CreateUser")]
        public ICollection<TOrder> TOrder { get; set; }


        //用户绑定的微信信息
        public ICollection<TUserBindWeixin> TUserBindWeixin { get; set; }


        //用户绑定的支付宝信息
        public ICollection<TUserBindAlipay> TUserBindAlipay { get; set; }


    }
}
