namespace AdminShared.Models.Role
{


    /// <summary>
    /// 角色功能
    /// </summary>
    public class DtoRoleFunction
    {

        /// <summary>
        /// 功能ID
        /// </summary>
        public long Id { get; set; }



        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }



        /// <summary>
        /// 类型,["模块","功能"]
        /// </summary>
        public string Type { get; set; }



        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsCheck { get; set; }



        /// <summary>
        /// 标记
        /// </summary>
        public string Sign { get; set; }



        /// <summary>
        /// 子集数据集合
        /// </summary>
        public List<DtoRoleFunction> ChildList { get; set; }


    }
}
