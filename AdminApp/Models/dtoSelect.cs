using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AdminApp.Models
{
    public class dtoSelect
    {


        /// <summary>
        /// 值
        /// </summary>
        public Guid Value { get; set; }


        /// <summary>
        /// 标签
        /// </summary>
        public string Label { get; set; }


        /// <summary>
        /// 是否禁用
        /// </summary>
        public bool IsDisabled { get; set; }


    }
}
