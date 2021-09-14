using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AdminApi.Models.v1.Link
{

    /// <summary>
    /// 友情链接
    /// </summary>
    public class dtoLink
    {


        /// <summary>
        /// 标识ID
        /// </summary>
        public Guid Id { get; set; }



        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }



        /// <summary>
        /// 网址
        /// </summary>
        public string Url { get; set; }



        /// <summary>
        /// 排序
        /// </summary>
        public int Sort { get; set; }


        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }

    }
}
