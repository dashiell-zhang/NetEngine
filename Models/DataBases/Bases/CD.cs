using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Models.DataBases.Bases
{

    /// <summary>
    /// 创建，删除
    /// </summary>
    public class CD
    {

        /// <summary>
        /// 主键标识ID
        /// </summary>
        [MaxLength(64)]
        public string Id { get; set; }


        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }



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
