using Authorize.Model.AppSetting;
using Common;
using DistributedLock;
using IdentifierGenerator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Repository.Database;
using System.Security.Claims;
using System.Security.Cryptography;
using WebAPI.Core.Interfaces;

namespace Admin.WebAPI.Services
{

    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class PermissionService : IPermissionService
    {


        /// <summary>
        /// 权限校验
        /// </summary>
        /// <param name="authorizationHandlerContext"></param>
        /// <returns></returns>
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

                    var db = httpContext.RequestServices.GetRequiredService<DatabaseContext>();

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
                            SigningCredentials signingCredentials = new(new ECDsaSecurityKey(jwtPrivateKey), SecurityAlgorithms.EcdsaSha256);

                            var nowTime = DateTime.UtcNow;

                            SecurityTokenDescriptor tokenDescriptor = new()
                            {
                                IssuedAt = nowTime,
                                Issuer = jwtSetting.Issuer,
                                Audience = jwtSetting.Audience,
                                NotBefore = nowTime,
                                Subject = new ClaimsIdentity(claims),
                                Expires = nowTime + jwtSetting.Expiry,
                                SigningCredentials = signingCredentials
                            };

                            JsonWebTokenHandler jwtTokenHandler = new();

                            var token = jwtTokenHandler.CreateToken(tokenDescriptor);


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
