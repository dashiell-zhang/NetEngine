using Microsoft.EntityFrameworkCore;
using Repository.Database.Bases;

namespace Repository.Database
{

    /// <summary>
    /// 文章表
    /// </summary>
    [Index(nameof(Title))]
    public class TArticle : CD_User
    {


        /// <summary>
        /// 类别ID
        /// </summary>
        public long CategoryId { get; set; }
        public virtual TCategory Category { get; set; }


        /// <summary>
        /// 标题
        /// </summary>
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


    }
}
