using System;

namespace AdminShared.Models.v1.Article
{

    /// <summary>
    /// 栏目信息
    /// </summary>
    public class DtoCategory
    {

        /// <summary>
        /// 标识ID
        /// </summary>
        public long Id { get; set; }



        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }



        /// <summary>
        /// 备注
        /// </summary>
        public string Remarks { get; set; }



        /// <summary>
        /// 排序
        /// </summary>
        public int Sort { get; set; }



        /// <summary>
        /// 父级信息
        /// </summary>
        public long? ParentId { get; set; }
        public string ParentName { get; set; }



        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }
    }
}
