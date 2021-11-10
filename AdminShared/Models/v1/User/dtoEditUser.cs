using System.ComponentModel.DataAnnotations;


namespace AdminShared.Models.v1.User
{


    public class dtoEditUser
    {


        /// <summary>
        /// 用户名
        /// </summary>
        [Required(ErrorMessage = "用户名不可以空")]
        public string Name { get; set; }


        /// <summary>
        /// 昵称
        /// </summary>
        [Required(ErrorMessage = "昵称不可以空")]
        public string NickName { get; set; }


        /// <summary>
        /// 手机号
        /// </summary>
        [Required(ErrorMessage = "手机号不可以空")]
        public string Phone { get; set; }


        /// <summary>
        /// 邮箱
        /// </summary>
        public string Email { get; set; }


        /// <summary>
        /// 密码
        /// </summary>
        [Required(ErrorMessage = "密码不可以为空")]
        public string PassWord { get; set; }

    }
}
