using System;
using System.Collections.Generic;
using System.Text;

namespace Models.DataBases.Bases
{

    /// <summary>
    /// 创建，编辑，删除
    /// </summary>
    public class CUD
    {

        /// <summary>
        /// 主键标识ID
        /// </summary>
        public string Id { get; set; }


        /// <summary>
        /// 标记
        /// </summary>
        public int Mark { get; set; }


        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }
        

      


        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime? UpdateTime { get; set; }


      


        /// <summary>
        /// 删除时间
        /// </summary>
        public DateTime? DeleteTime { get; set; }


      

    }
}
