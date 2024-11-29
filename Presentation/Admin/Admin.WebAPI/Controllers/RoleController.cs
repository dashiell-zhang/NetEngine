using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Model;
using User.Interface;
using User.Model.Role;
using WebAPI.Core.Filters;

namespace Admin.WebAPI.Controllers
{


    [SignVerifyFilter]
    [Route("[controller]/[action]")]
    [Authorize]
    [ApiController]
    public class RoleController(IRoleService roleService) : ControllerBase
    {


        /// <summary>
        /// 获取角色列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpGet]
        public DtoPageList<DtoRole> GetRoleList([FromQuery] DtoPageRequest request) => roleService.GetRoleList(request);



        /// <summary>
        /// 通过ID获取角色信息
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <returns></returns>
        [HttpGet]
        public DtoRole? GetRole(long roleId) => roleService.GetRole(roleId);



        /// <summary>
        /// 创建角色
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        [QueueLimitFilter(IsBlock = true, IsUseToken = true)]
        [HttpPost]
        public long CreateRole(DtoEditRole role) => roleService.CreateRole(role);



        /// <summary>
        /// 编辑角色
        /// </summary>
        /// <param name="roleId"></param>
        /// <param name="role"></param>
        /// <returns></returns>
        [QueueLimitFilter(IsBlock = true, IsUseToken = true)]
        [HttpPost]
        public bool UpdateRole(long roleId, DtoEditRole role) => roleService.UpdateRole(roleId, role);



        /// <summary>
        /// 删除角色
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        public bool DeleteRole(long id) => roleService.DeleteRole(id);



        /// <summary>
        /// 获取某个角色的功能权限
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <returns></returns>
        [HttpGet]
        public List<DtoRoleFunction> GetRoleFunction(long roleId) => roleService.GetRoleFunction(roleId);



        /// <summary>
        /// 设置角色的功能
        /// </summary>
        /// <param name="setRoleFunction"></param>
        /// <returns></returns>
        [HttpPost]
        public bool SetRoleFunction(DtoSetRoleFunction setRoleFunction) => roleService.SetRoleFunction(setRoleFunction);



        /// <summary>
        /// 获取角色键值对
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public List<DtoKeyValue> GetRoleKV() => roleService.GetRoleKV();

    }
}