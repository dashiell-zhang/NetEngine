using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorCms.Models.Home
{

    public class dtoLogin
    {

        [Required]
        public string Name { get; set; }


        [Required]
        public string PassWord { get; set; }

    }
}
