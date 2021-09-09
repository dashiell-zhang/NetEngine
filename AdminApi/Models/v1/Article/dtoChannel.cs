using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AdminApi.Models.v1.Article
{
    public class dtoChannel
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
        /// 备注
        /// </summary>
        public string Remarks { get; set; }



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
