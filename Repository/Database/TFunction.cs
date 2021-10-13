using Microsoft.EntityFrameworkCore;
using Repository.Bases;
using System;
using System.Collections.Generic;

namespace Repository.Database
{


    /// <summary>
    /// 系统功能配置表
    /// </summary>
    [Index(nameof(Sign))]
    public class TFunction : CD
    {


        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }



        /// <summary>
        /// 标记
        /// </summary>
        public string Sign { get; set; }



        /// <summary>
        /// 备注
        /// </summary>
        public string Remarks { get; set; }



        /// <summary>
        /// 父级信息
        /// </summary>
        public Guid? ParentId { get; set; }
        public virtual TFunction Parent { get; set; }



        /// <summary>
        /// 类型
        /// </summary>
        public enumType Type { get; set; }
        public enum enumType
        {
            模块 = 1,
            功能 = 2
        }



        /// <summary>
        /// 该功能动作集合
        /// </summary>
        public virtual List<TFunctionAction> TFunctionAction { get; set; }


    }
}
