using Repository.Bases;
using System;

namespace Repository.Database
{

    /// <summary>
    /// 功能模块对应Action记录表
    /// </summary>
    public class TFunctionAction : CD
    {


        /// <summary>
        /// 功能信息
        /// </summary>
        public Guid FunctionId { get; set; }
        public virtual TFunction Function { get; set; }



        /// <summary>
        /// 模块
        /// </summary>
        public string Module { get; set; }



        /// <summary>
        /// 控制器
        /// </summary>
        public string Controller { get; set; }



        /// <summary>
        /// 动作
        /// </summary>
        public string Action { get; set; }




        /// <summary>
        /// 备注
        /// </summary>
        public string Remarks { get; set; }


    }
}
