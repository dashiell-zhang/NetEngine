using Microsoft.EntityFrameworkCore;
using Repository.Bases;

namespace Repository.Database
{
    [Index(nameof(Table)), Index(nameof(TableId))]
    public class TDataUpdateLog : CD
    {



        /// <summary>
        /// 外链表名
        /// </summary>
        public string Table { get; set; }



        /// <summary>
        /// 外链表ID
        /// </summary>
        public long TableId { get; set; }



        /// <summary>
        /// 变动内容
        /// </summary>
        public string Content { get; set; }



        /// <summary>
        /// 操作人信息
        /// </summary>
        public long? ActionUserId { get; set; }
        public virtual TUser? ActionUser { get; set; }




        /// <summary>
        /// Ip地址
        /// </summary>
        public string? IpAddress { get; set; }



        /// <summary>
        ///  设备标记
        /// </summary>
        public string? DeviceMark { get; set; }


    }
}
