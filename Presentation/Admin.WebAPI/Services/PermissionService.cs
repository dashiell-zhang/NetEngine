using Application.Core.Interfaces.Authorize;
using Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using WebAPI.Core.Interfaces;

namespace Admin.WebAPI.Services
{

    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class PermissionService(IAuthorizeService authorizeService) : IPermissionService
    {

        public async Task<bool> VerifyAuthorizationAsync(AuthorizationHandlerContext authorizationHandlerContext)
        {
            if (authorizationHandlerContext.User.Identity!.IsAuthenticated)
            {
                if (authorizationHandlerContext.Resource is HttpContext httpContext)
                {
                    var newToken = await authorizeService.IssueNewTokenAsync();

                    if (newToken != null)
                    {
                        httpContext.Response.Headers.Append("NewToken", newToken);
                        httpContext.Response.Headers.Append("Access-Control-Expose-Headers", "NewToken");
                    }

                    var module = typeof(Program).Assembly.GetName().Name!;

                    Endpoint endpoint = httpContext.GetEndpoint()!;

                    ControllerActionDescriptor actionDescriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()!;

                    var route = actionDescriptor.AttributeRouteInfo?.Template!;

                    var checkResult = await authorizeService.CheckFunctionAuthorizeAsync(module, route);

                    return checkResult;
                }
            }

            return false;
        }

    }
}
