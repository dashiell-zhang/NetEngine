using System.ComponentModel.DataAnnotations;


namespace Admin.Model.User
{


    public class DtoEditUser
    {

        /// <summary>
        /// 名称
        /// </summary>
        [Required(ErrorMessage = "名称不可以空")]
        public string Name { get; set; }


        /// <summary>
        /// 用户名
        /// </summary>
        [Required(ErrorMessage = "用户名不可以空")]
        public string UserName { get; set; }


        /// <summary>
        /// 手机号
        /// </summary>
        [Required(ErrorMessage = "手机号不可以空")]
        public string Phone { get; set; }


        /// <summary>
        /// 邮箱
        /// </summary>
        public string? Email { get; set; }


        /// <summary>
        /// 密码
        /// </summary>
        [Required(ErrorMessage = "密码不可以为空")]
        public string Password { get; set; }



        /// <summary>
        /// 角色ID集合
        /// </summary>
        public string[] RoleIds { get; set; }

    }
}
