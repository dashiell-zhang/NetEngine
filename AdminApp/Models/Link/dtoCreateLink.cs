using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace AdminApp.Models.Link
{

    /// <summary>
    /// 创建友情链接
    /// </summary>
    public class dtoCreateLink
    {


        /// <summary>
        /// 名称
        /// </summary>
        [Required(ErrorMessage = "名称不可以空")]
        public string Name { get; set; }



        /// <summary>
        /// 网址
        /// </summary>
        [Required(ErrorMessage = "Url不可以空")]
        public string Url { get; set; }



        /// <summary>
        /// 排序
        /// </summary>
        public int Sort { get; set; }


    }
}
