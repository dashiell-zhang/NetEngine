using Common;
using DistributedLock;
using IdentifierGenerator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using Repository.Database;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using WebAPIBasic.Libraries;
using WebAPIBasic.Models.AppSetting;

namespace AdminAPI.Libraries
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

            if (authorizationHandlerContext.User.Identity!.IsAuthenticated)
            {

                if (authorizationHandlerContext.Resource is HttpContext httpContext)
                {

                    IssueNewToken(httpContext);

                    var module = typeof(Program).Assembly.GetName().Name!;

                    Endpoint endpoint = httpContext.GetEndpoint()!;

                    ControllerActionDescriptor actionDescriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()!;

                    var route = actionDescriptor.AttributeRouteInfo?.Template;

                    var db = httpContext.RequestServices.GetRequiredService<DatabaseContext>();

                    var functionId = db.TFunctionRoute.Where(t => t.Module == module && t.Route == route).Select(t => t.FunctionId).FirstOrDefault();

                    if (functionId != default)
                    {
                        var userId = long.Parse(httpContext.GetClaimByUser("userId")!);
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

                return true;
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

            IdService idService = httpContext.RequestServices.GetRequiredService<IdService>();

            var db = httpContext.RequestServices.GetRequiredService<DatabaseContext>();

            var nbf = Convert.ToInt64(httpContext.GetClaimByUser("nbf"));
            var exp = Convert.ToInt64(httpContext.GetClaimByUser("exp"));

            var nbfTime = DateTimeOffset.FromUnixTimeSeconds(nbf);
            var expTime = DateTimeOffset.FromUnixTimeSeconds(exp);

            var lifeSpan = nbfTime + ((expTime - nbfTime) * 0.5);

            //当前Token有效期不足一半时签发新的Token
            if (lifeSpan < DateTimeOffset.UtcNow)
            {

                var tokenId = long.Parse(httpContext.GetClaimByUser("tokenId")!);
                var userId = long.Parse(httpContext.GetClaimByUser("userId")!);


                string key = "IssueNewToken" + tokenId;

                var distLock = httpContext.RequestServices.GetRequiredService<IDistributedLock>();
                var cache = httpContext.RequestServices.GetRequiredService<IDistributedCache>();

                if (distLock.TryLock(key) != null)
                {
                    var newToken = db.TUserToken.Where(t => t.LastId == tokenId && t.CreateTime > nbfTime).FirstOrDefault();

                    if (newToken == null)
                    {
                        var tokenInfo = db.TUserToken.Where(t => t.Id == tokenId).FirstOrDefault();

                        if (tokenInfo != null)
                        {

                            TUserToken userToken = new()
                            {
                                Id = idService.GetId(),
                                UserId = userId,
                                LastId = tokenId
                            };

                            var claims = new Claim[]{
                                    new("tokenId",userToken.Id.ToString()),
                                    new("userId",userId.ToString())
                                };


                            var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
                            var jwtSetting = configuration.GetRequiredSection("JWT").Get<JWTSetting>()!;
                            var jwtPrivateKey = ECDsa.Create();
                            jwtPrivateKey.ImportECPrivateKey(Convert.FromBase64String(jwtSetting.PrivateKey), out _);
                            SigningCredentials creds = new(new ECDsaSecurityKey(jwtPrivateKey), SecurityAlgorithms.EcdsaSha256);
                            JwtSecurityToken jwtSecurityToken = new(jwtSetting.Issuer, jwtSetting.Audience, claims, DateTime.UtcNow, DateTime.UtcNow + jwtSetting.Expiry, creds);

                            var token = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken);

                            db.TUserToken.Add(userToken);

                            if (distLock.TryLock("ClearExpireToken") != null)
                            {
                                var clearTime = DateTime.UtcNow.AddDays(-7);
                                var clearList = db.TUserToken.Where(t => t.CreateTime < clearTime).ToList();
                                db.TUserToken.RemoveRange(clearList);
                            }

                            db.SaveChanges();

                            cache.Set(userToken.Id + "token", token, TimeSpan.FromMinutes(10));

                            httpContext.Response.Headers.Append("NewToken", token);
                            httpContext.Response.Headers.Append("Access-Control-Expose-Headers", "NewToken");
                        }
                    }
                    else
                    {
                        var token = cache.GetString(newToken.Id + "token");
                        httpContext.Response.Headers.Append("NewToken", token);
                        httpContext.Response.Headers.Append("Access-Control-Expose-Headers", "NewToken");
                    }
                }
            }

        }



    }
}
