using Application.Model.Shared;
using Application.Model.User.Role;

namespace Application.Interface.User;

/// <summary>
/// 角色与角色功能授权管理
/// </summary>
public interface IRoleService
{

    /// <summary>
    /// 获取角色列表
    /// </summary>
    Task<PageListDto<RoleDto>> GetRoleListAsync(PageRequestDto request);


    /// <summary>
    /// 通过ID获取角色信息
    /// </summary>
    Task<RoleDto?> GetRoleAsync(long roleId);


    /// <summary>
    /// 创建角色
    /// </summary>
    Task<long> CreateRoleAsync(EditRoleDto role);


    /// <summary>
    /// 编辑角色
    /// </summary>
    Task<bool> UpdateRoleAsync(long roleId, EditRoleDto role);


    /// <summary>
    /// 删除角色
    /// </summary>
    Task<bool> DeleteRoleAsync(long id);


    /// <summary>
    /// 获取某个角色的功能权限
    /// </summary>
    Task<List<RoleFunctionDto>> GetRoleFunctionAsync(long roleId);


    /// <summary>
    /// 设置角色的功能
    /// </summary>
    Task<bool> SetRoleFunctionAsync(long actorUserId, SetRoleFunctionDto setRoleFunction);


    /// <summary>
    /// 获取角色选项列表
    /// </summary>
    Task<List<RoleSelectDto>> GetRoleSelectAsync();

}
