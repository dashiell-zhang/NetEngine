using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Models.Dtos;
using Repository.Database;
using System;
using System.Linq;

namespace Cms.Libraries.Verify
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
                        var modular = "cms";

                        var endpoint = httpContext.GetEndpoint();

                        var actionDescriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();

                        var controller = actionDescriptor.ControllerName.ToLower();
                        var action = actionDescriptor.ActionName.ToLower();

                        using (var db = new dbContext())
                        {

                            var userIdStr = httpContext.User.Claims.ToList().Where(t => t.Type == "userId").Select(t => t.Value).FirstOrDefault();

                            var userId = Guid.Parse(userIdStr);
                            var roleIds = db.TUserRole.Where(t => t.IsDelete == false & t.UserId == userId).Select(t => t.RoleId).ToList();

                            var functionId = db.TFunctionAction.Where(t => t.IsDelete == false & t.Modular.ToLower() == modular & t.Controller.ToLower() == controller & t.Action.ToLower() == action).Select(t => t.FunctionId).FirstOrDefault();

                            if (functionId != default)
                            {
                                var functionAuthorizeId = db.TFunctionAuthorize.Where(t => t.IsDelete == false & t.FunctionId == functionId & (roleIds.Contains(t.RoleId) | t.UserId == userId)).Select(t => t.Id).FirstOrDefault();

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




        /// <summary>
        /// 校验短信身份验证码
        /// </summary>
        /// <param name="keyValue">key 为手机号，value 为验证码</param>
        /// <returns></returns>
        public static bool SmsVerifyPhone(dtoKeyValue keyValue)
        {
            string phone = keyValue.Key.ToString();

            string key = "VerifyPhone_" + phone;

            var code = Common.RedisHelper.StringGet(key);

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
