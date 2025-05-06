using Application.Model.Shared;
using Application.Model.User.Role;

namespace Application.Core.Interfaces.User
{
    public interface IRoleService
    {

        /// 获取角色列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        Task<DtoPageList<DtoRole>> GetRoleListAsync(DtoPageRequest request);


        /// <summary>
        /// 通过ID获取角色信息
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <returns></returns>
        Task<DtoRole?> GetRoleAsync(long roleId);


        /// <summary>
        /// 创建角色
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        Task<long> CreateRoleAsync(DtoEditRole role);


        /// <summary>
        /// 编辑角色
        /// </summary>
        /// <param name="roleId"></param>
        /// <param name="role"></param>
        /// <returns></returns>
        Task<bool> UpdateRoleAsync(long roleId, DtoEditRole role);


        /// <summary>
        /// 删除角色
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<bool> DeleteRoleAsync(long id);


        /// <summary>
        /// 获取某个角色的功能权限
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <returns></returns>
        Task<List<DtoRoleFunction>> GetRoleFunctionAsync(long roleId);


        /// <summary>
        /// 设置角色的功能
        /// </summary>
        /// <param name="setRoleFunction"></param>
        /// <returns></returns>
        Task<bool> SetRoleFunctionAsync(DtoSetRoleFunction setRoleFunction);


        /// <summary>
        /// 获取角色键值对
        /// </summary>
        /// <returns></returns>
        Task<List<DtoKeyValue>> GetRoleKVAsync();


        /// <summary>
        /// 获取某个角色某个功能下的子集功能
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <param name="parentId">功能父级ID</param>
        /// <returns></returns>
        Task<List<DtoRoleFunction>> GetRoleFunctionChildListAsync(long roleId, long parentId);

    }
}
