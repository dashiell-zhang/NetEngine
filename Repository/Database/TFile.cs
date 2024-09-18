using Microsoft.EntityFrameworkCore;
using Repository.Database.Bases;

namespace Repository.Database
{


    /// <summary>
    /// 文件表
    /// </summary>
    [Index(nameof(Table)), Index(nameof(TableId)), Index(nameof(Sign))]
    public class TFile : CD_User
    {


        /// <summary>
        /// 文件名称
        /// </summary>
        public string Name { get; set; }


        /// <summary>
        /// 保存路径
        /// </summary>
        public string Path { get; set; }



        /// <summary>
        /// 文件大小
        /// </summary>
        public long Length { get; set; }



        /// <summary>
        /// 是否支持公开访问
        /// </summary>
        public bool IsPublicRead { get; set; }



        /// <summary>
        /// 外链表名
        /// </summary>
        public string Table { get; set; }


        /// <summary>
        /// 外链表ID
        /// </summary>
        public long? TableId { get; set; }


        /// <summary>
        /// 标记
        /// </summary>
        public string Sign { get; set; }


        /// <summary>
        /// 排序
        /// </summary>
        public int Sort { get; set; }
    }
}
