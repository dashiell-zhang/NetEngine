using Models.DataBases.Bases;
using Models.DataBases.WebCore.Bases;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models.DataBases.WebCore
{


    /// <summary>
    /// 文件表
    /// </summary>
    [Table("t_file")]
    public class TFile :CD_User
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
        /// 文件类型
        /// </summary>
        public string Type { get; set; } 

    }
}
