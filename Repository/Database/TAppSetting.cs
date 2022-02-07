using Microsoft.EntityFrameworkCore;
using Repository.Bases;
using System;

namespace Repository.Database
{


    /// <summary>
    /// 系统配置信息表
    /// </summary>
    [Index(nameof(Module))]
    public class TAppSetting : CD
    {


        public TAppSetting(string module, string key, string value)
        {
            Module = module;
            Key = key;
            Value = value;
        }


        /// <summary>
        /// 模块
        /// </summary>
        public string Module { get; set; }



        /// <summary>
        /// 组ID
        /// </summary>
        public long? GroupId { get; set; }



        /// <summary>
        /// 键名
        /// </summary>
        public string Key { get; set; }




        /// <summary>
        /// 键值
        /// </summary>
        public string Value { get; set; }


    }
}
