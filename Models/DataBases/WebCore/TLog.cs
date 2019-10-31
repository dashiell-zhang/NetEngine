using Models.DataBases.Bases;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Models.DataBases.WebCore
{

    /// <summary>
    /// 日志表
    /// </summary>
    [Table("t_log")]
    public class TLog:CD
    {


        /// <summary>
        /// 标记
        /// </summary>
        public string Sign { get; set; }


        /// <summary>
        /// 类型
        /// </summary>
        public string Type { get; set; }



        /// <summary>
        /// 内容
        /// </summary>
        public string Content { get; set; }

    }
}
