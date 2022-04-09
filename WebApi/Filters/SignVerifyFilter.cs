using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Cryptography;

namespace WebApi.Filters
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


            var filter = (SignVerifyFilter)context.Filters.Where(t => t.ToString() == (typeof(SignVerifyFilter).Assembly.GetName().Name + ".Filters.SignVerifyFilter")).ToList().LastOrDefault()!;


            if (!filter.IsSkip)
            {
                var token = context.HttpContext.Request.Headers["Token"].ToString().ToLower();

                var rip = context.HttpContext.Connection.RemoteIpAddress!.ToString();

                if (!rip.Contains("127.0.0.1"))
                {
                    var timeStr = context.HttpContext.Request.Headers["Time"].ToString();
                    var time = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(timeStr));

                    if (time.AddMinutes(10) > DateTime.UtcNow)
                    {

                        var authorizationStr = context.HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                        var securityToken = new JwtSecurityToken(authorizationStr);

                        string privateKey = securityToken.RawSignature;

                        string dataStr = privateKey + timeStr;


                        if (context.HttpContext.Request.Method == "POST")
                        {
                            if (context.HttpContext.Request.HasFormContentType)
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
                                        using var md5 = MD5.Create();

                                        var fileMd5 = BitConverter.ToString(md5.ComputeHash(fileStream)).Replace("-", "").ToLower();

                                        dataStr = dataStr + file.Name + fileMd5;
                                    }
                                }

                            }
                            else
                            {
                                string body = Libraries.Http.HttpContext.GetRequestBody();

                                dataStr += body;
                            }
                        }
                        else if (context.HttpContext.Request.Method == "GET")
                        {
                            var queryList = context.HttpContext.Request.Query.OrderBy(t => t.Key).ToList();

                            foreach (var query in queryList)
                            {
                                string qv = query.Value;
                                dataStr = dataStr + query.Key + qv;
                            }
                        }


                        string tk = Common.CryptoHelper.GetMd5(dataStr).ToLower();

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
