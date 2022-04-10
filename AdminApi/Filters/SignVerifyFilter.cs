using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Cryptography;

namespace AdminApi.Filters
{

    /// <summary>
    /// 签名过滤器
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class SignVerifyFilter : Attribute, IActionFilter
    {


        /// <summary>
        /// 是否跳过签名验证，可用于控制器下单个接口指定跳过签名验证
        /// </summary>
        public bool IsSkip { get; set; }


        void IActionFilter.OnActionExecuting(ActionExecutingContext context)
        {
            SignVerifyFilter filter = (SignVerifyFilter)context.Filters.Where(t => t.ToString() == (typeof(SignVerifyFilter).Assembly.GetName().Name + ".Filters.SignVerifyFilter")).ToList().LastOrDefault()!;

            if (!filter.IsSkip)
            {
                var token = context.HttpContext.Request.Headers["Token"].ToString();

                var remoteIpAddress = context.HttpContext.Connection.RemoteIpAddress!.ToString();

                if (!remoteIpAddress.Contains("127.0.0.1"))
                {
                    var timeStr = context.HttpContext.Request.Headers["Time"].ToString();
                    var time = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(timeStr));

                    if (time.AddSeconds(30) > DateTime.UtcNow)
                    {

                        var authorizationStr = context.HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                        var securityToken = new JwtSecurityToken(authorizationStr);

                        string privateKey = securityToken.RawSignature;

                        string dataStr = privateKey + timeStr;

                        var requestUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;

                        dataStr = dataStr + requestUrl;

                        if (!context.HttpContext.Request.HasFormContentType)
                        {
                            string body = Libraries.Http.HttpContext.GetRequestBody();
                            dataStr += body;
                        }
                        else
                        {
                            var fromlist = context.HttpContext.Request.Form.OrderBy(t => t.Key).ToList();

                            foreach (var fm in fromlist)
                            {
                                string fmv = fm.Value.ToString();
                                dataStr = dataStr + fm.Key + fmv;
                            }

                            var files = context.HttpContext.Request.Form.Files.OrderBy(t => t.Name).ToList();

                            foreach (var file in files)
                            {
                                using (var fileStream = file.OpenReadStream())
                                {
                                    using var sha256 = SHA256.Create();

                                    var fileSign = BitConverter.ToString(sha256.ComputeHash(fileStream)).Replace("-", "");

                                    dataStr = dataStr + file.Name + fileSign;
                                }
                            }
                        }

                        string tk = Common.CryptoHelper.GetSHA256(dataStr);

                        if (token != tk)
                        {
                            context.HttpContext.Response.StatusCode = 401;

                            context.Result = new JsonResult(new { errMsg = "非法 Token" });
                        }
                    }
                    else
                    {
                        context.HttpContext.Response.StatusCode = 401;
                        context.Result = new JsonResult(new { errMsg = "Token 已过期" });
                    }
                }
            }
        }


        void IActionFilter.OnActionExecuted(ActionExecutedContext context)
        {

        }
    }
}
