using System.ComponentModel.DataAnnotations;

namespace WebApi.Models.Authorize
{
    public class DtoUpdatePassWordByOldPassWord
    {


        /// <summary>
        /// 旧的密码
        /// </summary>
        [Required(ErrorMessage = "旧的密码不可以为空")]
        public string OldPassWord { get; set; }



        /// <summary>
        /// 新的密码
        /// </summary>
        [Required(ErrorMessage = "新的密码不可以为空")]
        public string NewPassWord { get; set; }

    }
}
