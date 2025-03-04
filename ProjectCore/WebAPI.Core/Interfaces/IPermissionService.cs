using Microsoft.AspNetCore.Authorization;

namespace WebAPI.Core.Interfaces
{
    public interface IPermissionService
    {

        /// <summary>
        /// 权限校验
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        Task<bool> VerifyAuthorizationAsync(AuthorizationHandlerContext context);

    }
}
