using Common;
using DistributedLock;
using IdentifierGenerator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Caching.Distributed;
using Repository.Database;
using Shared.Interface;
using System.Security.Claims;
using WebAPI.Core.Interfaces;

namespace Client.WebAPI.Services
{

    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class PermissionService : IPermissionService
    {


        public bool VerifyAuthorization(AuthorizationHandlerContext authorizationHandlerContext)
        {

            if (authorizationHandlerContext.User.Identity!.IsAuthenticated)
            {

                if (authorizationHandlerContext.Resource is HttpContext httpContext)
                {

                    IssueNewToken(httpContext);

                    var module = typeof(Program).Assembly.GetName().Name!;

                    Endpoint endpoint = httpContext.GetEndpoint()!;

                    ControllerActionDescriptor actionDescriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()!;

                    var route = actionDescriptor.AttributeRouteInfo?.Template;

                    using var db = httpContext.RequestServices.GetRequiredService<DatabaseContext>();

                    var functionId = db.TFunctionRoute.Where(t => t.Module == module && t.Route == route).Select(t => t.FunctionId).FirstOrDefault();

                    if (functionId != default)
                    {
                        var userId = long.Parse(httpContext.User.FindFirstValue("userId")!);
                        var roleIds = db.TUserRole.Where(t => t.UserId == userId).Select(t => t.RoleId).ToList();

                        var functionAuthorizeId = db.TFunctionAuthorize.Where(t => t.FunctionId == functionId && (roleIds.Contains(t.RoleId!.Value) || t.UserId == userId)).Select(t => t.Id).FirstOrDefault();

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

                return false;
            }
            else
            {
                return false;
            }

        }



        /// <summary>
        /// 签发新Token
        /// </summary>
        /// <param name="httpContext"></param>
        private static void IssueNewToken(HttpContext httpContext)
        {

            var idService = httpContext.RequestServices.GetRequiredService<IdService>();

            var db = httpContext.RequestServices.GetRequiredService<DatabaseContext>();

            var nbf = long.Parse(httpContext.User.FindFirstValue("nbf")!);
            var exp = long.Parse(httpContext.User.FindFirstValue("exp")!);

            var nbfTime = DateTimeOffset.FromUnixTimeSeconds(nbf);
            var expTime = DateTimeOffset.FromUnixTimeSeconds(exp);

            var lifeSpan = nbfTime + (expTime - nbfTime) * 0.5;

            //当前Token有效期不足一半时签发新的Token
            if (lifeSpan < DateTimeOffset.UtcNow)
            {

                var tokenId = long.Parse(httpContext.User.FindFirstValue("tokenId")!);
                var userId = long.Parse(httpContext.User.FindFirstValue("userId")!);

                var distLock = httpContext.RequestServices.GetRequiredService<IDistributedLock>();
                var cache = httpContext.RequestServices.GetRequiredService<IDistributedCache>();

                string key = "IssueNewToken" + tokenId;
                if (distLock.TryLock(key) != null)
                {

                    var newToken = cache.GetString(tokenId + "newToken");

                    if (newToken == null)
                    {
                        var authorizeService = httpContext.RequestServices.GetRequiredService<IAuthorizeService>();

                        newToken = authorizeService.GetTokenByUserId(userId, tokenId);

                        if (distLock.TryLock("ClearExpireToken") != null)
                        {
                            var clearTime = DateTime.UtcNow.AddDays(-7);
                            var clearList = db.TUserToken.Where(t => t.CreateTime < clearTime).ToList();
                            db.TUserToken.RemoveRange(clearList);

                            db.SaveChanges();
                        }

                        cache.Set(tokenId + "newToken", newToken, TimeSpan.FromMinutes(10));
                    }

                    httpContext.Response.Headers.Append("NewToken", newToken);
                    httpContext.Response.Headers.Append("Access-Control-Expose-Headers", "NewToken");  //解决 Ionic 取不到 Header中的信息问题
                }

            }

        }


    }
}
