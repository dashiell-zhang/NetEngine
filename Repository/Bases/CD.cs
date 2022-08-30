using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.Bases
{

    /// <summary>
    /// 创建，删除
    /// </summary>
    [Index(nameof(CreateTime)), Index(nameof(DeleteTime))]
    public class CD
    {

        /// <summary>
        /// 主键标识ID
        /// </summary>
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { get; set; }


        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTimeOffset CreateTime { get; set; }



        /// <summary>
        /// 是否删除
        /// </summary>
        public bool IsDelete { get; set; }



        /// <summary>
        /// 删除时间
        /// </summary>
        public DateTimeOffset? DeleteTime { get; set; }



        /// <summary>
        /// 行版本标记
        /// </summary>
#pragma warning disable IDE1006 // 命名样式
        public uint xmin { get; set; }
#pragma warning restore IDE1006 // 命名样式


    }
}
