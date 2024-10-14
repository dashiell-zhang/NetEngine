using System.ComponentModel.DataAnnotations;

namespace Admin.Model.Article
{

    /// <summary>
    /// 创建栏目
    /// </summary>
    public class DtoEditCategory
    {


        /// <summary>
        /// 名称
        /// </summary>
        [Required(ErrorMessage = "名称不可以空")]
        public string Name { get; set; }



        /// <summary>
        /// 父级ID
        /// </summary>
        public long? ParentId { get; set; }



        /// <summary>
        /// 备注
        /// </summary>
        public string? Remarks { get; set; }



        /// <summary>
        /// 排序
        /// </summary>
        public int Sort { get; set; }


    }
}
