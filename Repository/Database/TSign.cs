using Microsoft.EntityFrameworkCore;
using Repository.Database.Bases;
using System;

namespace Repository.Database
{

    /// <summary>
    /// 点赞或标记喜欢记录表
    /// </summary>
    [Index(nameof(Table)), Index(nameof(TableId)), Index(nameof(Sign))]
    public class TSign : CD_User
    {


        public TSign(string table, string sign)
        {
            Table = table;
            Sign = sign;
        }


        /// <summary>
        /// 外链表名称
        /// </summary>
        public string Table { get; set; }



        /// <summary>
        /// 外链记录ID
        /// </summary>
        public long TableId { get; set; }



        /// <summary>
        /// 自定义标记
        /// </summary>
        public string Sign { get; set; }

    }
}
