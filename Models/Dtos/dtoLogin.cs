using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Models.Dtos
{

    /// <summary>
    /// 登录信息
    /// </summary>
    public class dtoLogin
    {

        /// <summary>
        /// 用户名
        /// </summary>
        [Required]
        public string name { get; set; }



        /// <summary>
        /// 密码
        /// </summary>
        [Required]
        public string password { get; set; }
    }
}
