using Microsoft.EntityFrameworkCore;
using Repository.Bases;

namespace Repository.Database
{

    /// <summary>
    /// 功能模块对应Action记录表
    /// </summary>
    [Index(nameof(Module)), Index(nameof(Controller)), Index(nameof(Action))]
    public class TFunctionAction : CD
    {


        /// <summary>
        /// 功能信息
        /// </summary>
        public long FunctionId { get; set; }
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
        public string? Remarks { get; set; }


    }
}
