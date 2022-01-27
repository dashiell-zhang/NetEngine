using Common;
using Medallion.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using WebApi.Models.Shared;

namespace WebApi.Libraries.Verify
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

                    IssueNewToken(httpContext);

                    var module = "webapi";

                    var endpoint = httpContext.GetEndpoint();

                    var actionDescriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();

                    var controller = actionDescriptor.ControllerName.ToLower();
                    var action = actionDescriptor.ActionName.ToLower();

                    using var scope = Program.ServiceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetService<DatabaseContext>();

                    var userId = long.Parse(JWTToken.GetClaims("userId"));
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

            var snowflakeHelper = httpContext.RequestServices.GetService<SnowflakeHelper>();

            using var scope = Program.ServiceProvider.CreateScope();
            var db = scope.ServiceProvider.GetService<DatabaseContext>();

            var nbf = Convert.ToInt64(JWTToken.GetClaims("nbf"));
            var exp = Convert.ToInt64(JWTToken.GetClaims("exp"));

            var nbfTime = DateTimeOffset.FromUnixTimeSeconds(nbf);
            var expTime = DateTimeOffset.FromUnixTimeSeconds(exp);

            //当前Token过期前15分钟开始签发新的Token
            if (expTime < DateTime.UtcNow.AddMinutes(15))
            {

                var tokenId = long.Parse(JWTToken.GetClaims("tokenId"));
                var userId = long.Parse(JWTToken.GetClaims("userId"));


                string key = "IssueNewToken" + tokenId;

                var distLock = httpContext.RequestServices.GetService<IDistributedLockProvider>();
                if (distLock.TryAcquireLock(key) != null)
                {
                    var newToken = db.TUserToken.Where(t => t.IsDelete == false & t.LastId == tokenId & t.CreateTime > nbfTime).FirstOrDefault();

                    if (newToken == null)
                    {
                        var tokenInfo = db.TUserToken.Where(t => t.Id == tokenId).FirstOrDefault();

                        if (tokenInfo != null)
                        {

                            TUserToken userToken = new();
                            userToken.Id = snowflakeHelper.GetId();
                            userToken.UserId = userId;
                            userToken.LastId = tokenId;
                            userToken.CreateTime = DateTime.UtcNow;

                            var claims = new Claim[]{
                                    new Claim("tokenId",userToken.Id.ToString()),
                                    new Claim("userId",userId.ToString())
                                };


                            var token = JWTToken.GetToken(claims);

                            userToken.Token = token;

                            db.TUserToken.Add(userToken);

                            db.SaveChanges();

#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
                            ClearExpireToken();
#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法

                            httpContext.Response.Headers.Add("NewToken", token);
                            httpContext.Response.Headers.Add("Access-Control-Expose-Headers", "NewToken");  //解决 Ionic 取不到 Header中的信息问题
                        }
                    }
                    else
                    {
                        httpContext.Response.Headers.Add("NewToken", newToken.Token);
                        httpContext.Response.Headers.Add("Access-Control-Expose-Headers", "NewToken");  //解决 Ionic 取不到 Header中的信息问题
                    }
                }

            }

        }




        /// <summary>
        /// 清理过期Token
        /// </summary>
        private static async Task ClearExpireToken()
        {
            await Task.Run(() =>
            {
                var distLock = Program.ServiceProvider.GetService<IDistributedLockProvider>();
                if (distLock.TryAcquireLock("ClearExpireToken") != null)
                {
                    using var scope = Program.ServiceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetService<DatabaseContext>();

                    var clearTime = DateTime.UtcNow.AddDays(-7);
                    var clearList = db.TUserToken.Where(t => t.CreateTime < clearTime).ToList();
                    db.TUserToken.RemoveRange(clearList);

                    db.SaveChanges();
                }
            });
        }




        /// <summary>
        /// 校验短信身份验证码
        /// </summary>
        /// <param name="keyValue">key 为手机号，value 为验证码</param>
        /// <returns></returns>
        public static bool SmsVerifyPhone(DtoKeyValue keyValue)
        {
            string phone = keyValue.Key.ToString()!;

            string key = "VerifyPhone_" + phone;

            var code = Common.CacheHelper.GetString(key);

            if (string.IsNullOrEmpty(code) == false && code == keyValue.Value.ToString())
            {
                return true;
            }
            else
            {
                return false;
            }
        }


    }
}
