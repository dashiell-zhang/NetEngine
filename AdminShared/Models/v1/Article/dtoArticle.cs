using System;
using System.Collections.Generic;

namespace AdminShared.Models.v1.Article
{

    /// <summary>
    /// 文章数据结构
    /// </summary>
    public class dtoArticle
    {

        /// <summary>
        /// 标识ID
        /// </summary>
        public Guid Id { get; set; }



        /// <summary>
        /// 类别信息
        /// </summary>
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; }



        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; set; }



        /// <summary>
        /// 内容
        /// </summary>
        public string Content { get; set; }



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
        public string Abstract { get; set; }



        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }



        /// <summary>
        /// 封面图
        /// </summary>
        public List<dtoKeyValue> CoverImageList { get; set; }


    }
}
