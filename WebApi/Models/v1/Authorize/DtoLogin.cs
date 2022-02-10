using System.ComponentModel.DataAnnotations;

namespace WebApi.Models.v1.Authorize
{

    /// <summary>
    /// 登录信息
    /// </summary>
    public class DtoLogin
    {



        /// <summary>
        /// 用户名
        /// </summary>
        [Required(ErrorMessage = "用户名不可以空")]
        public string Name { get; set; }




        /// <summary>
        /// 密码
        /// </summary>
        [Required(ErrorMessage = "密码不可以为空")]
        public string PassWord { get; set; }


    }
}
