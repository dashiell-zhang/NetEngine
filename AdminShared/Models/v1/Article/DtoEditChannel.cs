using System.ComponentModel.DataAnnotations;

namespace AdminShared.Models.v1.Article
{
    public class DtoEditChannel
    {


        /// <summary>
        /// 名称
        /// </summary>
        [Required(ErrorMessage = "名称不可以空")]
        public string Name { get; set; }



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
