using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Model;
using User.Interface;
using User.Model.User;
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
    public class UserController(IUserService userService) : ControllerBase
    {


        /// <summary>
        /// 获取用户列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpGet]
        public DtoPageList<DtoUser> GetUserList([FromQuery] DtoPageRequest request) => userService.GetUserList(request);



        /// <summary>
        /// 通过 UserId 获取用户信息 
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns></returns>
        [HttpGet]
        public DtoUser? GetUser(long? userId) => userService?.GetUser(userId);



        /// <summary>
        /// 创建用户
        /// </summary>
        /// <param name="createUser"></param>
        /// <returns></returns>
        [HttpPost]
        public long? CreateUser(DtoEditUser createUser) => userService.CreateUser(createUser);



        /// <summary>
        /// 更新用户信息
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="updateUser"></param>
        /// <returns></returns>
        [HttpPost]
        public bool UpdateUser(long userId, DtoEditUser updateUser) => userService.UpdateUser(userId, updateUser);



        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        public bool DeleteUser(long id) => userService.DeleteUser(id);



        /// <summary>
        /// 获取某个用户的功能权限
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns></returns>
        [HttpGet]
        public List<DtoUserFunction> GetUserFunction(long userId) => userService.GetUserFunction(userId);



        /// <summary>
        /// 设置用户的功能
        /// </summary>
        /// <param name="setUserFunction"></param>
        /// <returns></returns>
        [QueueLimitFilter()]
        [HttpPost]
        public bool SetUserFunction(DtoSetUserFunction setUserFunction) => userService.SetUserFunction(setUserFunction);


        /// <summary>
        /// 获取用户角色列表
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        [HttpGet]
        public List<DtoUserRole> GetUserRoleList(long userId) => userService.GetUserRoleList(userId);



        /// <summary>
        /// 设置用户角色
        /// </summary>
        /// <param name="setUserRole"></param>
        /// <returns></returns>
        [QueueLimitFilter()]
        [HttpPost]
        public bool SetUserRole(DtoSetUserRole setUserRole) => userService.SetUserRole(setUserRole);


    }
}