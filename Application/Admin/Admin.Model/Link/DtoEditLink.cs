using System.ComponentModel.DataAnnotations;

namespace Admin.Model.Link
{

    /// <summary>
    /// 更新友情链接
    /// </summary>
    public class DtoEditLink
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
