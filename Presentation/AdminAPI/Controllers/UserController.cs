using Admin.Interface;
using AdminShared.Models;
using AdminShared.Models.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPIBasic.Filters;

namespace AdminAPI.Controllers
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
        public DtoPageList<DtoUser> GetUserList([FromQuery] DtoPageRequest request)
        {
            return userService.GetUserList(request);
        }



        /// <summary>
        /// 通过 UserId 获取用户信息 
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns></returns>
        [HttpGet]
        public DtoUser? GetUser(long? userId)
        {
            return userService?.GetUser(userId);
        }



        /// <summary>
        /// 创建用户
        /// </summary>
        /// <param name="createUser"></param>
        /// <returns></returns>
        [HttpPost]
        public long? CreateUser(DtoEditUser createUser)
        {
            return userService.CreateUser(createUser);
        }



        /// <summary>
        /// 更新用户信息
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="updateUser"></param>
        /// <returns></returns>
        [HttpPost]
        public bool UpdateUser(long userId, DtoEditUser updateUser)
        {
            return userService.UpdateUser(userId, updateUser);
        }



        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        public bool DeleteUser(long id)
        {
            return userService.DeleteUser(id);
        }



        /// <summary>
        /// 获取某个用户的功能权限
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns></returns>
        [HttpGet]
        public List<DtoUserFunction> GetUserFunction(long userId)
        {
            return userService.GetUserFunction(userId);
        }



        /// <summary>
        /// 设置用户的功能
        /// </summary>
        /// <param name="setUserFunction"></param>
        /// <returns></returns>
        [QueueLimitFilter()]
        [HttpPost]
        public bool SetUserFunction(DtoSetUserFunction setUserFunction)
        {
            return userService.SetUserFunction(setUserFunction);
        }



        /// <summary>
        /// 获取用户角色列表
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        [HttpGet]
        public List<DtoUserRole> GetUserRoleList(long userId)
        {
            return userService.GetUserRoleList(userId);
        }



        /// <summary>
        /// 设置用户角色
        /// </summary>
        /// <param name="setUserRole"></param>
        /// <returns></returns>
        [QueueLimitFilter()]
        [HttpPost]
        public bool SetUserRole(DtoSetUserRole setUserRole)
        {
            return userService.SetUserRole(setUserRole);
        }



    }
}