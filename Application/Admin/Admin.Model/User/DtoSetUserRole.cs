namespace Admin.Model.User
{

    /// <summary>
    /// 设置用户角色入参
    /// </summary>
    public class DtoSetUserRole
    {


        /// <summary>
        /// 角色ID
        /// </summary>
        public long RoleId { get; set; }



        /// <summary>
        /// 用户ID
        /// </summary>
        public long UserId { get; set; }



        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsCheck { get; set; }
    }
}
