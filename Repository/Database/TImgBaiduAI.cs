using Microsoft.EntityFrameworkCore;
using Repository.Bases;
using System;

namespace Repository.Database
{

    /// <summary>
    /// 产品图片对应百度AI信息表
    /// </summary>
    [Index(nameof(Unique))]
    public class TImgBaiduAI : CUD
    {


        /// <summary>
        /// 图片文件ID
        /// </summary>
        public long FileId { get; set; }
        public virtual TFile File { get; set; }


        /// <summary>
        /// 图片库唯一标识符
        /// </summary>
        public string Unique { get; set; }


        /// <summary>
        /// 接口返回值
        /// </summary>
        public string Result { get; set; }

    }
}
