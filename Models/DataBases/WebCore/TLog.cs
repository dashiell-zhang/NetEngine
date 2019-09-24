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
        public string type { get; set; }



        /// <summary>
        /// 计数
        /// </summary>
        public int count { get; set; }

    }
}
