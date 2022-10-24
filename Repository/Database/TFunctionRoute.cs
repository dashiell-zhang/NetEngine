using Repository.Bases;

namespace Repository.Database
{

    /// <summary>
    /// 功能模块对应路由记录表
    /// </summary>
    public class TFunctionRoute : CD
    {


        /// <summary>
        /// 功能信息
        /// </summary>
        public long? FunctionId { get; set; }
        public virtual TFunction? Function { get; set; }



        /// <summary>
        /// 模块
        /// </summary>
        public string Module { get; set; }




        /// <summary>
        /// 路由
        /// </summary>
        public string Route { get; set; }




        /// <summary>
        /// 备注
        /// </summary>
        public string? Remarks { get; set; }


    }
}
