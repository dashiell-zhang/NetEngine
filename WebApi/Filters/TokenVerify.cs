using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Linq;

namespace WebApi.Filters
{

    /// <summary>
    /// 预设固定 PrivateKey 双方验签 Token 验证方法
    /// </summary>
    public class TokenVerify : Attribute, IActionFilter
    {

        void IActionFilter.OnActionExecuting(ActionExecutingContext context)
        {
            var token = context.HttpContext.Request.Headers["Token"].ToString().ToLower();

            var rip = context.HttpContext.Connection.RemoteIpAddress.ToString();

            if (!rip.Contains("127.0.0.1"))
            {
                var timeStr = context.HttpContext.Request.Headers["Time"].ToString();
                var time = Convert.ToDateTime(timeStr);

                if (time.AddMinutes(10) > DateTime.Now)
                {
                    string privatekey = "gejinet";

                    string strdata = privatekey + timeStr;


                    if (context.HttpContext.Request.Method == "POST")
                    {
                        string body = Methods.Http.HttpContext.GetBody();

                        if (!string.IsNullOrEmpty(body))
                        {
                            strdata = strdata + body;
                        }
                        else if (context.HttpContext.Request.HasFormContentType)
                        {
                            var fromlist = context.HttpContext.Request.Form.ToList();

                            foreach (var fm in fromlist)
                            {
                                strdata = strdata + fm.Key + ":" + fm.Value.ToString();
                            }
                        }
                    }
                    else if (context.HttpContext.Request.Method == "GET")
                    {
                        var qrStr = context.HttpContext.Request.Query.ToString();

                        strdata = strdata + qrStr;
                    }


                    string tk = Methods.Crypto.Md5.GetMd5(strdata.ToLower()).ToLower();

                    if (token != tk)
                    {
                        context.HttpContext.Response.StatusCode = 401;

                        context.Result = new JsonResult(new { errMsg = "非法 Token ！" });
                    }
                }
                else
                {
                    context.HttpContext.Response.StatusCode = 401;
                    context.Result = new JsonResult(new { errMsg = "Token 有效期以过！" });
                }
            }

        }


        void IActionFilter.OnActionExecuted(ActionExecutedContext context)
        {

        }
    }
}
