﻿using Microsoft.EntityFrameworkCore;
using Repository.Bases;
using System;

namespace Repository.Database
{
    [Index(nameof(Table)), Index(nameof(TableId)), Index(nameof(Sign))]
    public class TOSLog : CD
    {


        public TOSLog(string table, string sign, string content)
        {
            Table = table;
            Sign = sign;
            Content = content;
        }


        /// <summary>
        /// 外链表名
        /// </summary>
        public string Table { get; set; }



        /// <summary>
        /// 外链表ID
        /// </summary>
        public long TableId { get; set; }



        /// <summary>
        /// 标记
        /// </summary>
        public string Sign { get; set; }



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
        /// 备注
        /// </summary>
        public string? Remarks { get; set; }



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
