using Microsoft.EntityFrameworkCore;
using Repository.Database.Bases;

namespace Repository.Database
{

    /// <summary>
    /// 栏目信息表
    /// </summary>
    [Index(nameof(Name))]
    public class TCategory : CD_User
    {


        /// <summary>
        /// 栏目名目
        /// </summary>
        public string Name { get; set; }


        /// <summary>
        /// 排序
        /// </summary>
        public int Sort { get; set; }


        /// <summary>
        /// 父级栏目ID
        /// </summary>
        public long? ParentId { get; set; }
        public virtual TCategory? Parent { get; set; }


        /// <summary>
        /// 备注
        /// </summary>
        public string? Remarks { get; set; }


        public virtual List<TArticle>? TArticle { get; set; }
    }
}
