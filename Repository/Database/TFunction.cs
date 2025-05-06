using Microsoft.EntityFrameworkCore;
using Repository.Bases;
using Repository.Enum;

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
        public string? Remarks { get; set; }


        /// <summary>
        /// 父级信息
        /// </summary>
        public long? ParentId { get; set; }
        public virtual TFunction? Parent { get; set; }


        /// <summary>
        /// 功能类型
        /// </summary>
        public EnumFunctionType Type { get; set; }


        public virtual List<TFunctionRoute>? TFunctionRoute { get; set; }

    }
}
