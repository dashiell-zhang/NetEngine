using Admin.Model.Role;
using Shared.Model;

namespace Admin.Interface
{
    public interface IRoleService
    {


        /// 获取角色列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public DtoPageList<DtoRole> GetRoleList(DtoPageRequest request);



        /// <summary>
        /// 通过ID获取角色信息
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <returns></returns>
        public DtoRole? GetRole(long roleId);



        /// <summary>
        /// 创建角色
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        public long CreateRole(DtoEditRole role);


        /// <summary>
        /// 编辑角色
        /// </summary>
        /// <param name="roleId"></param>
        /// <param name="role"></param>
        /// <returns></returns>
        public bool UpdateRole(long roleId, DtoEditRole role);



        /// <summary>
        /// 删除角色
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool DeleteRole(long id);



        /// <summary>
        /// 获取某个角色的功能权限
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <returns></returns>
        public List<DtoRoleFunction> GetRoleFunction(long roleId);



        /// <summary>
        /// 设置角色的功能
        /// </summary>
        /// <param name="setRoleFunction"></param>
        /// <returns></returns>
        public bool SetRoleFunction(DtoSetRoleFunction setRoleFunction);



        /// <summary>
        /// 获取角色键值对
        /// </summary>
        /// <returns></returns>
        public List<DtoKeyValue> GetRoleKV();



        /// <summary>
        /// 获取某个角色某个功能下的子集功能
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <param name="parentId">功能父级ID</param>
        /// <returns></returns>
        public List<DtoRoleFunction> GetRoleFunctionChildList(long roleId, long parentId);

    }
}
