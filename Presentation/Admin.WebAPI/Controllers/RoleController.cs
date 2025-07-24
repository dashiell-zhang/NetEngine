using Application.Interface.User;
using Application.Model.Shared;
using Application.Model.User.Role;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        public Task<DtoPageList<DtoRole>> GetRoleList([FromQuery] DtoPageRequest request) => roleService.GetRoleListAsync(request);



        /// <summary>
        /// 通过ID获取角色信息
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <returns></returns>
        [HttpGet]
        public Task<DtoRole?> GetRole(long roleId) => roleService.GetRoleAsync(roleId);



        /// <summary>
        /// 创建角色
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        [QueueLimitFilter(IsBlock = true, IsUseToken = true)]
        [HttpPost]
        public Task<long> CreateRole(DtoEditRole role) => roleService.CreateRoleAsync(role);



        /// <summary>
        /// 编辑角色
        /// </summary>
        /// <param name="roleId"></param>
        /// <param name="role"></param>
        /// <returns></returns>
        [QueueLimitFilter(IsBlock = true, IsUseToken = true)]
        [HttpPost]
        public Task<bool> UpdateRole(long roleId, DtoEditRole role) => roleService.UpdateRoleAsync(roleId, role);



        /// <summary>
        /// 删除角色
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        public Task<bool> DeleteRole(long id) => roleService.DeleteRoleAsync(id);



        /// <summary>
        /// 获取某个角色的功能权限
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <returns></returns>
        [HttpGet]
        public Task<List<DtoRoleFunction>> GetRoleFunction(long roleId) => roleService.GetRoleFunctionAsync(roleId);



        /// <summary>
        /// 设置角色的功能
        /// </summary>
        /// <param name="setRoleFunction"></param>
        /// <returns></returns>
        [HttpPost]
        public Task<bool> SetRoleFunction(DtoSetRoleFunction setRoleFunction) => roleService.SetRoleFunctionAsync(setRoleFunction);



        /// <summary>
        /// 获取角色键值对
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public Task<List<DtoKeyValue>> GetRoleKV() => roleService.GetRoleKVAsync();

    }
}