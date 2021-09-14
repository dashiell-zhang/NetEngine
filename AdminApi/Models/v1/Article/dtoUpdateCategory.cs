using System;

namespace AdminApi.Models.v1.Article
{

    /// <summary>
    /// 更新栏目
    /// </summary>
    public class dtoUpdateCategory
    {


        /// <summary>
        /// 标识ID
        /// </summary>
        public Guid Id { get; set; }



        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }



        /// <summary>
        /// 父级ID
        /// </summary>
        public Guid? ParentId { get; set; }



        /// <summary>
        /// 备注
        /// </summary>
        public string Remarks { get; set; }



        /// <summary>
        /// 排序
        /// </summary>
        public int Sort { get; set; }


    }
}
