using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebApi.Models
{
    public class Run
    {
        /// <summary>
        /// 调用方法名称
        /// </summary>
        public string run { get; set; }


        /// <summary>
        /// 发起方租户ID
        /// </summary>

        public int uid { get; set; }



        /// <summary>
        /// 数据加密后的key
        /// </summary>

        public string key { get; set; }


        /// <summary>
        /// 序列化后的业务数据
        /// </summary>

        public string data { get; set; }
    }


    public class RunResult
    {

        /// <summary>
        /// 执行状态
        /// </summary>
        public int status { get; set; }


        /// <summary>
        /// 批注消息
        /// </summary>
        public string msg { get; set; }



        /// <summary>
        /// 序列化后的业务数据
        /// </summary>
        public string data { get; set; }
    }
}
