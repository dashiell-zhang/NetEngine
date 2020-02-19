using Repository.WebCore.Bases;
using System.Collections.Generic;

namespace Repository.WebCore
{


    /// <summary>
    /// 频道信息表
    /// </summary>
    public class TChannel:CD_User
    {

        /// <summary>
        /// 频道名称
        /// </summary>
        public string Name { get; set; }
        
        
        /// <summary>
        /// 排序
        /// </summary>
        public int Sort { get; set; }
        
        
        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; }




        /// <summary>
        /// 所包含的类别记录数据
        /// </summary>
        public ICollection<TCategory> TCategory { get; set; }
    }
}
