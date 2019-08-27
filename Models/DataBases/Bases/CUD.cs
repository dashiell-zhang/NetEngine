using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
        [MaxLength(64)]
        public string Id { get; set; }


        /// <summary>
        /// 标记
        /// </summary>
        public string Mark { get; set; }


        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }


        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime? UpdateTime { get; set; }


        /// <summary>
        /// 是否删除
        /// </summary>
        [Column(TypeName = "bit")]
        public bool? IsDelete { get; set; }


        /// <summary>
        /// 删除时间
        /// </summary>
        public DateTime? DeleteTime { get; set; }


      

    }
}
