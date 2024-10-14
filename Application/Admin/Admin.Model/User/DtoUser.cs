namespace Admin.Model.User
{
    public class DtoUser
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
        /// 用户名
        /// </summary>
        public string UserName { get; set; }


        /// <summary>
        /// 手机号
        /// </summary>
        public string Phone { get; set; }


        /// <summary>
        /// 邮箱
        /// </summary>
        public string? Email { get; set; }


        /// <summary>
        /// 角色
        /// </summary>
        public string? Roles { get; set; }



        /// <summary>
        /// 角色ID集合
        /// </summary>
        public string[]? RoleIds { get; set; }


        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTimeOffset CreateTime { get; set; }

    }
}
