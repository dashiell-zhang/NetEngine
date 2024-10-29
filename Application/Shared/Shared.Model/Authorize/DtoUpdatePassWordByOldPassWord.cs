using System.ComponentModel.DataAnnotations;

namespace Shared.Model.Authorize
{
    public class DtoUpdatePasswordByOldPassword
    {


        /// <summary>
        /// 旧的密码
        /// </summary>
        [Required(ErrorMessage = "旧的密码不可以为空")]
        public string OldPassword { get; set; }



        /// <summary>
        /// 新的密码
        /// </summary>
        [Required(ErrorMessage = "新的密码不可以为空")]
        public string NewPassword { get; set; }

    }
}
