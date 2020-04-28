using Repository.Database.Bases;
using System.Collections.Generic;

namespace Repository.Database
{

    /// <summary>
    /// 栏目信息表
    /// </summary>
    public class TCategory : CD_User
    {


        /// <summary>
        /// 频道ID
        /// </summary>
        public string ChannelId { get; set; }
        public TChannel Channel { get; set; }


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
        public string ParentId { get; set; }
        public virtual TCategory Parent { get; set; }


        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; }




        public virtual ICollection<TArticle> TArticle { get; set; }
    }
}
