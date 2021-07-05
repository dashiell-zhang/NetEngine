using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.Bases
{

    /// <summary>
    /// 创建，删除
    /// </summary>
    public class CD
    {

        /// <summary>
        /// 主键标识ID
        /// </summary>
        public Guid Id { get; set; }


        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }



        /// <summary>
        /// 是否删除
        /// </summary>
        [ConcurrencyCheck]
        public bool IsDelete { get; set; }



        /// <summary>
        /// 删除时间
        /// </summary>
        [ConcurrencyCheck]
        public DateTime? DeleteTime { get; set; }

    }
}
