using System.ComponentModel.DataAnnotations;

namespace Admin.Model.Role
{
    public class DtoEditRole
    {


        /// <summary>
        /// 角色名称
        /// </summary>
        [Required(ErrorMessage = "名称不可以空")]
        public string Name { get; set; }



        /// <summary>
        /// 备注信息
        /// </summary>
        public string? Remarks { get; set; }

    }
}
