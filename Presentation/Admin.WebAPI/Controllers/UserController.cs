using Application.Model.Shared;
using Application.Model.User.User;
using Application.Service.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Core.Filters;

namespace Admin.WebAPI.Controllers
{

    /// <summary>
    /// 用户数据操作控制器
    /// </summary>
    [SignVerifyFilter]
    [Route("[controller]/[action]")]
    [Authorize]
    [ApiController]
    public class UserController(UserService userService) : ControllerBase
    {

        /// <summary>
        /// 获取用户列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpGet]
        public Task<DtoPageList<DtoUser>> GetUserList([FromQuery] DtoPageRequest request) => userService.GetUserListAsync(request);


        /// <summary>
        /// 通过 UserId 获取用户信息 
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns></returns>
        [HttpGet]
        public Task<DtoUser?> GetUser(long? userId) => userService.GetUserAsync(userId);


        /// <summary>
        /// 创建用户
        /// </summary>
        /// <param name="createUser"></param>
        /// <returns></returns>
        [HttpPost]
        public Task<long?> CreateUser(DtoEditUser createUser) => userService.CreateUserAsync(createUser);


        /// <summary>
        /// 更新用户信息
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="updateUser"></param>
        /// <returns></returns>
        [HttpPost]
        public Task<bool> UpdateUser(long userId, DtoEditUser updateUser) => userService.UpdateUserAsync(userId, updateUser);


        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        public Task<bool> DeleteUser(long id) => userService.DeleteUserAsync(id);


        /// <summary>
        /// 获取某个用户的功能权限
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns></returns>
        [HttpGet]
        public Task<List<DtoUserFunction>> GetUserFunction(long userId) => userService.GetUserFunctionAsync(userId);


        /// <summary>
        /// 设置用户的功能
        /// </summary>
        /// <param name="setUserFunction"></param>
        /// <returns></returns>
        [QueueLimitFilter()]
        [HttpPost]
        public Task<bool> SetUserFunction(DtoSetUserFunction setUserFunction) => userService.SetUserFunctionAsync(setUserFunction);


        /// <summary>
        /// 获取用户角色列表
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        [HttpGet]
        public Task<List<DtoUserRole>> GetUserRoleList(long userId) => userService.GetUserRoleListAsync(userId);


        /// <summary>
        /// 设置用户角色
        /// </summary>
        /// <param name="setUserRole"></param>
        /// <returns></returns>
        [QueueLimitFilter()]
        [HttpPost]
        public Task<bool> SetUserRole(DtoSetUserRole setUserRole) => userService.SetUserRoleAsync(setUserRole);

    }
}