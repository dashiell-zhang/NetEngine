using Application.Model.Shared;
using Application.Model.User.User;

namespace Application.Interface.User;

/// <summary>
/// 用户资料、角色与功能授权管理
/// </summary>
public interface IUserService
{

    /// <summary>
    /// 通过 UserId 获取用户信息 
    /// </summary>
    Task<UserDto?> GetUserAsync(long targetUserId);


    /// <summary>
    /// 通过短信验证码修改账户手机号
    /// </summary>
    Task<bool> EditUserPhoneBySmsAsync(long actorUserId, EditUserPhoneBySmsDto request);


    /// <summary>
    /// 获取用户列表
    /// </summary>
    Task<PageListDto<UserDto>> GetUserListAsync(UserPageRequestDto request);


    /// <summary>
    /// 创建用户
    /// </summary>
    Task<long?> CreateUserAsync(long actorUserId, EditUserDto createUser);


    /// <summary>
    /// 更新用户信息
    /// </summary>
    Task<bool> UpdateUserAsync(long actorUserId, long targetUserId, EditUserDto updateUser);


    /// <summary>
    /// 删除用户
    /// </summary>
    Task<bool> DeleteUserAsync(long actorUserId, long targetUserId);


    /// <summary>
    /// 获取某个用户的功能权限
    /// </summary>
    Task<List<UserFunctionDto>> GetUserFunctionAsync(long userId);


    /// <summary>
    /// 设置用户的功能
    /// </summary>
    Task<bool> SetUserFunctionAsync(long actorUserId, SetUserFunctionDto setUserFunction);


    /// <summary>
    /// 获取用户角色列表
    /// </summary>
    Task<List<UserRoleDto>> GetUserRoleListAsync(long userId);


    /// <summary>
    /// 设置用户角色
    /// </summary>
    Task<bool> SetUserRoleAsync(long actorUserId, SetUserRoleDto setUserRole);

}
