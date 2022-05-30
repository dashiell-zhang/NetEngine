using AdminShared.Models;
using Common;
using Common.DistributedLock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using System;
using System.Linq;
using System.Security.Claims;

namespace AdminApi.Libraries.Verify
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

                    var module = "webapi";

                    Endpoint endpoint = httpContext.GetEndpoint()!;

                    ControllerActionDescriptor actionDescriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()!;

                    var controller = actionDescriptor.ControllerName.ToLower();
                    var action = actionDescriptor.ActionName.ToLower();

                    var db = httpContext.RequestServices.GetRequiredService<DatabaseContext>();

                    var userId = long.Parse(JWTToken.GetClaims("userId")!);
                    var roleIds = db.TUserRole.Where(t => t.IsDelete == false && t.UserId == userId).Select(t => t.RoleId).ToList();

                    var functionId = db.TFunctionAction.Where(t => t.IsDelete == false && t.Module.ToLower() == module && t.Controller.ToLower() == controller && t.Action.ToLower() == action).Select(t => t.FunctionId).FirstOrDefault();

                    if (functionId != default)
                    {
                        var functionAuthorizeId = db.TFunctionAuthorize.Where(t => t.IsDelete == false && t.FunctionId == functionId && (roleIds.Contains(t.RoleId!.Value) || t.UserId == userId)).Select(t => t.Id).FirstOrDefault();

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

            SnowflakeHelper snowflakeHelper = httpContext.RequestServices.GetRequiredService<SnowflakeHelper>();

            var db = httpContext.RequestServices.GetRequiredService<DatabaseContext>();

            var nbf = Convert.ToInt64(JWTToken.GetClaims("nbf"));
            var exp = Convert.ToInt64(JWTToken.GetClaims("exp"));

            var nbfTime = DateTimeOffset.FromUnixTimeSeconds(nbf);
            var expTime = DateTimeOffset.FromUnixTimeSeconds(exp);

            //当前Token过期前15分钟开始签发新的Token
            if (expTime < DateTime.UtcNow.AddMinutes(15))
            {

                var tokenId = long.Parse(JWTToken.GetClaims("tokenId")!);
                var userId = long.Parse(JWTToken.GetClaims("userId")!);


                string key = "IssueNewToken" + tokenId;

                var distLock = httpContext.RequestServices.GetRequiredService<IDistributedLock>();
                if (distLock.TryLock(key) != null)
                {
                    var newToken = db.TUserToken.Where(t => t.IsDelete == false && t.LastId == tokenId && t.CreateTime > nbfTime).FirstOrDefault();

                    if (newToken == null)
                    {
                        var tokenInfo = db.TUserToken.Where(t => t.Id == tokenId).FirstOrDefault();

                        if (tokenInfo != null)
                        {

                            TUserToken userToken = new()
                            {
                                Id = snowflakeHelper.GetId(),
                                UserId = userId,
                                LastId = tokenId,
                                CreateTime = DateTime.UtcNow
                            };

                            var claims = new Claim[]{
                                    new Claim("tokenId",userToken.Id.ToString()),
                                    new Claim("userId",userId.ToString())
                                };


                            var token = JWTToken.GetToken(claims);

                            userToken.Token = token;

                            db.TUserToken.Add(userToken);

                            if (distLock.TryLock("ClearExpireToken") != null)
                            {
                                var clearTime = DateTime.UtcNow.AddDays(-7);
                                var clearList = db.TUserToken.Where(t => t.CreateTime < clearTime).ToList();
                                db.TUserToken.RemoveRange(clearList);
                            }

                            db.SaveChanges();


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
        /// 校验短信身份验证码
        /// </summary>
        /// <param name="keyValue">key 为手机号，value 为验证码</param>
        /// <returns></returns>
        public static bool SmsVerifyPhone(DtoKeyValue keyValue)
        {
            string phone = keyValue.Key?.ToString()!;

            string key = "VerifyPhone_" + phone;

            var code = Common.CacheHelper.GetString(key);

            if (string.IsNullOrEmpty(code) == false && code == keyValue.Value?.ToString())
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
