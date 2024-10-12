using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Interface;
using Web.Interface.Models.User;
using WebAPIBasic.Filters;
using WebAPIBasic.Models.Shared;

namespace WebAPI.Controllers
{


    /// <summary>
    /// 用户数据操作控制器
    /// </summary>
    [Route("[controller]/[action]")]
    [Authorize]
    [ApiController]
    public class UserController(IUserService userService) : ControllerBase
    {


        /// <summary>
        /// 通过 UserId 获取用户信息 
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns></returns>
        [HttpGet]
        [CacheDataFilter(TTL = 60, IsUseToken = true)]
        public DtoUser? GetUser(long? userId)
        {
            return userService.GetUser(userId);
        }



        /// <summary>
        /// 通过短信验证码修改账户手机号
        /// </summary>
        /// <param name="keyValue">key 为新手机号，value 为短信验证码</param>
        /// <returns></returns>
        [HttpPost]
        public bool EditUserPhoneBySms(DtoKeyValue keyValue)
        {
            return userService.EditUserPhoneBySms(keyValue);
        }


    }
}