namespace Admin.Model.Role
{
    public class DtoRole
    {


        /// <summary>
        /// 标识ID
        /// </summary>
        public long Id { get; set; }




        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTimeOffset CreateTime { get; set; }




        /// <summary>
        /// 角色名称
        /// </summary>
        public string Name { get; set; }




        /// <summary>
        /// 备注信息
        /// </summary>
        public string? Remarks { get; set; }

    }
}
