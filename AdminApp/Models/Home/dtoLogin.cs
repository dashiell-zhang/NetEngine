using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace AdminApp.Models.Home
{

    public class dtoLogin
    {

        [Required(ErrorMessage = "用户名不可以空")]
        public string Name { get; set; }


        [Required(ErrorMessage = "密码不可以空")]
        public string PassWord { get; set; }

    }
}
