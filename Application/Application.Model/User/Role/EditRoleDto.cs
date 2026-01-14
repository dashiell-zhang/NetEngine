using System.ComponentModel.DataAnnotations;

namespace Application.Model.User.Role;
public class EditRoleDto
{

    /// <summary>
    /// 角色编码
    /// </summary>
    [Required(ErrorMessage = "角色编码不可以空")]
    public string Code { get; set; }


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
