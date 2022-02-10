using Repository.Attributes;
using Repository.Bases;

namespace Repository.Database
{

    /// <summary>
    /// 角色信息表
    /// </summary>
    public class TRole : CD
    {



        /// <summary>
        /// 角色名称
        /// </summary>
        [ForeignName]
        public string Name { get; set; }


        /// <summary>
        /// 备注信息
        /// </summary>
        public string? Remarks { get; set; }



    }
}
