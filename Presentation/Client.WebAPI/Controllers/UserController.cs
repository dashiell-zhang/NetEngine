using Application.Model.User.User;
using Application.Service.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Core.Filters;

namespace Client.WebAPI.Controllers;


/// <summary>
/// 用户数据操作控制器
/// </summary>
[Route("[controller]/[action]")]
[Authorize]
[ApiController]
public class UserController(UserService userService) : ControllerBase
{


    /// <summary>
    /// 通过 UserId 获取用户信息 
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns></returns>
    [HttpGet]
    [CacheDataFilter(TTL = 60, IsUseToken = true)]
    public Task<UserDto?> GetUser(long? userId) => userService.GetUserAsync(userId);



    /// <summary>
    /// 通过短信验证码修改账户手机号
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost]
    public Task<bool> EditUserPhoneBySms(EditUserPhoneBySmsDto request) => userService.EditUserPhoneBySmsAsync(request);


}
