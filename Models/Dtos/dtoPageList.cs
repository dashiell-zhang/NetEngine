using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Models.Dtos
{


    /// <summary>
    /// 分页数据信息基类
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class dtoPageList<T>
    {


        /// <summary>
        /// 数据总量
        /// </summary>
        public int total { get; set; }




        /// <summary>
        /// 具体数据内容
        /// </summary>
        public List<T> list { get; set; }
    }
}
