using Repository.Bases;
using System;
using System.Collections.Generic;

namespace Repository.Database
{

    /// <summary>
    /// 产品图片表
    /// </summary>
    public class TProductImg:CUD
    {

        /// <summary>
        /// 产品ID
        /// </summary>
        public Guid ProductId { get; set; }
        public TProduct Product { get; set; }


        /// <summary>
        /// 图片文件ID
        /// </summary>
        public Guid FileId { get; set; }
        public TFile File { get; set; }


        /// <summary>
        /// 排序
        /// </summary>
        public int? Sort { get; set; }


        public  ICollection<TProductImgBaiduAi> ProductImgBaiduAis { get; set; }


    }
}
