using Shared.Model;
using System.ComponentModel.DataAnnotations;

namespace Admin.Model.Article
{

    /// <summary>
    /// 文章数据结构
    /// </summary>
    public class DtoArticle
    {



        /// <summary>
        /// 标识ID
        /// </summary>
        public long Id { get; set; }



        /// <summary>
        /// 类别信息
        /// </summary>
        public long CategoryId { get; set; }
        public string CategoryName { get; set; }



        /// <summary>
        /// 标题
        /// </summary>
        [Required(ErrorMessage = "标题不可以空")]
        public string Title { get; set; }



        /// <summary>
        /// 内容
        /// </summary>
        public string? Content { get; set; }



        /// <summary>
        /// 是否推荐
        /// </summary>
        public bool IsRecommend { get; set; }



        /// <summary>
        /// 是否显示
        /// </summary>
        public bool IsDisplay { get; set; }



        /// <summary>
        /// 排序
        /// </summary>
        public int Sort { get; set; }



        /// <summary>
        /// 点击数
        /// </summary>
        public int ClickCount { get; set; }



        /// <summary>
        /// 摘要
        /// </summary>
        public string? Digest { get; set; }



        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTimeOffset CreateTime { get; set; }



        /// <summary>
        /// 封面图
        /// </summary>
        public List<DtoKeyValue>? CoverImageList { get; set; }


    }
}
