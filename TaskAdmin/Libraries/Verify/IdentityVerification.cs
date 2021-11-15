using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using System.Linq;

namespace TaskAdmin.Libraries.Verify
{

    /// <summary>
    /// 认证模块静态方法
    /// </summary>
    public class IdentityVerification
    {


        /// <summary>
        /// 权限校验
        /// </summary>
        /// <param name="authorizationHandlerContext"></param>
        /// <returns></returns>
        public static bool Authorization(AuthorizationHandlerContext authorizationHandlerContext)
        {

            if (authorizationHandlerContext.User.Identity.IsAuthenticated)
            {

                if (authorizationHandlerContext.Resource is HttpContext httpContext)
                {

                    if (!string.IsNullOrEmpty(httpContext.Session.GetString("userId")))
                    {
                        var module = "taskadmin";

                        var endpoint = httpContext.GetEndpoint();

                        var actionDescriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();

                        var controller = actionDescriptor.ControllerName.ToLower();
                        var action = actionDescriptor.ActionName.ToLower();

                        using (var scope = httpContext.RequestServices.CreateScope())
                        {
                            var db = scope.ServiceProvider.GetService<dbContext>();

                            var userIdStr = httpContext.User.Claims.ToList().Where(t => t.Type == "userId").Select(t => t.Value).FirstOrDefault();

                            var userId = long.Parse(userIdStr);
                            var roleIds = db.TUserRole.Where(t => t.IsDelete == false & t.UserId == userId).Select(t => t.RoleId).ToList();

                            var functionId = db.TFunctionAction.Where(t => t.IsDelete == false & t.Module.ToLower() == module & t.Controller.ToLower() == controller & t.Action.ToLower() == action).Select(t => t.FunctionId).FirstOrDefault();

                            if (functionId != default)
                            {
                                var functionAuthorizeId = db.TFunctionAuthorize.Where(t => t.IsDelete == false & t.FunctionId == functionId & (roleIds.Contains(t.RoleId.Value) | t.UserId == userId)).Select(t => t.Id).FirstOrDefault();

                                if (functionAuthorizeId != default)
                                {
                                    return true;
                                }
                                else
                                {
                                    return false;
                                }
                            }
                            else
                            {
                                return true;
                            }

                        }
                    }
                    else
                    {
                        return false;
                    }
                }

                return true;
            }
            else
            {
                return false;
            }

        }





    }
}
