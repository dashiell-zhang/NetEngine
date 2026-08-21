using System.Security.Claims;

namespace WebAPI.Core.Extensions;

/// <summary>
/// 提供当前请求用户身份解析能力
/// </summary>
public static class ClaimsPrincipalExtension
{

    /// <summary>
    /// 获取当前认证用户ID
    /// </summary>
    /// <param name="user">当前请求用户</param>
    /// <returns>当前认证用户ID</returns>
    public static long GetUserId(this ClaimsPrincipal user)
    {

        if (user.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException("当前用户未通过认证");
        }

        var userIdClaim = user.FindFirstValue("userId");

        if (long.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        throw new InvalidOperationException("当前上下文中无有效的 UserId");

    }



    /// <summary>
    /// 获取可空的当前认证用户ID
    /// </summary>
    /// <param name="user">当前请求用户</param>
    /// <returns>已认证时返回用户ID 否则返回空</returns>
    public static long? GetUserIdOrNull(this ClaimsPrincipal user)
    {

        return user.Identity?.IsAuthenticated == true ? user.GetUserId() : null;

    }

}
