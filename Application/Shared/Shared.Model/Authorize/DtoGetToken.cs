using System.ComponentModel.DataAnnotations;

namespace Shared.Model.Authorize
{

    /// <summary>
    /// 获取Token
    /// </summary>
    public class DtoGetToken
    {


        /// <summary>
        /// 用户名
        /// </summary>
        [Required(ErrorMessage = "用户名不可以空")]
        public string UserName { get; set; }



        /// <summary>
        /// 密码
        /// </summary>
        [Required(ErrorMessage = "密码不可以为空")]
        public string Password { get; set; }


    }
}
