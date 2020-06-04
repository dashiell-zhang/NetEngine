﻿using Repository.Database.Bases;
using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.Database
{

    /// <summary>
    /// 文章表
    /// </summary>
    public class TArticle : CD_User
    {

        /// <summary>
        /// 类别ID
        /// </summary>
        public string CategoryId { get; set; }
        public TCategory Category { get; set; }


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
        [Column(TypeName = "bit")]
        public bool IsRecommend { get; set; }


        /// <summary>
        /// 是否显示
        /// </summary>
        [Column(TypeName = "bit")]
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


    }
}