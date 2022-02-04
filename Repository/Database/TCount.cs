using Microsoft.EntityFrameworkCore;
using Repository.Bases;

namespace Repository.Database
{

    /// <summary>
    /// 计数表
    /// </summary>
    [Index(nameof(Tag))]
    public class TCount : CUD
    {

        public TCount(string tag)
        {
            Tag = tag;
        }



        /// <summary>
        /// 标记
        /// </summary>
        public string Tag { get; set; }



        /// <summary>
        /// 计数
        /// </summary>
        public int Count { get; set; }

    }
}
