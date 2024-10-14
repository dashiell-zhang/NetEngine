namespace Admin.Model.User
{
    public class DtoUserRole
    {

        /// <summary>
        /// 角色id
        /// </summary>
        public long Id { get; set; }



        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }



        /// <summary>
        /// 备注
        /// </summary>
        public string? Remarks { get; set; }



        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsCheck { get; set; }


    }
}
