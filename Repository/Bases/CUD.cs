using Microsoft.EntityFrameworkCore;

namespace Repository.Bases
{

    /// <summary>
    /// 创建，编辑，删除
    /// </summary>
    [Index(nameof(UpdateTime))]
    public class CUD : CD
    {


        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTimeOffset? UpdateTime { get; set; }

    }
}
