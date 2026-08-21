using Application.Service;
using Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using SourceGenerator.Runtime.Attributes;
using WebAPI.Core.Extensions;
using WebAPI.Core.Interfaces;

namespace Admin.WebAPI.Services;

/// <summary>
/// 提供管理端请求的功能权限校验能力
/// </summary>
[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class PermissionService(AuthorizeService authorizeService) : IPermissionService
{

    /// <summary>
    /// 校验当前管理端请求的功能权限
    /// </summary>
    /// <param name="authorizationHandlerContext">授权处理上下文</param>
    /// <returns>是否通过授权</returns>
    public async Task<bool> VerifyAuthorizationAsync(AuthorizationHandlerContext authorizationHandlerContext)
    {
        if (authorizationHandlerContext.User.Identity!.IsAuthenticated)
        {
            if (authorizationHandlerContext.Resource is HttpContext httpContext)
            {
                var claims = authorizationHandlerContext.User.Claims;
                var actorUserId = authorizationHandlerContext.User.GetUserId();
                var tokenId = long.Parse(claims.First(t => t.Type == "tokenId").Value);
                var notBefore = long.Parse(claims.First(t => t.Type == "nbf").Value);
                var expiresAt = long.Parse(claims.First(t => t.Type == "exp").Value);
                var newToken = await authorizeService.IssueNewTokenAsync(actorUserId, tokenId, notBefore, expiresAt);

                if (newToken != null)
                {
                    httpContext.Response.Headers.Append("NewToken", newToken);
                    httpContext.Response.Headers.Append("Access-Control-Expose-Headers", "NewToken");
                }

                var module = typeof(Program).Assembly.GetName().Name!;

                Endpoint endpoint = httpContext.GetEndpoint()!;

                ControllerActionDescriptor actionDescriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()!;

                var route = actionDescriptor.AttributeRouteInfo?.Template!;

                var checkResult = await authorizeService.CheckFunctionAuthorizeAsync(actorUserId, module, route);

                return checkResult;
            }
        }

        return false;
    }

}
