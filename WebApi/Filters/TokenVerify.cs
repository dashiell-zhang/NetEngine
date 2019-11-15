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


        /// <summary>
        /// 是否跳过Token验证，可用于控制器下单个接口指定跳过Token验证
        /// </summary>
        public bool IsSkip { get; set; }


        void IActionFilter.OnActionExecuting(ActionExecutingContext context)
        {

            var filter = (TokenVerify)context.Filters.Where(t => t.ToString() == "WebApi.Filters.TokenVerify").ToList().LastOrDefault();


            if (!filter.IsSkip)
            {
                var token = context.HttpContext.Request.Headers["Token"].ToString().ToLower();

                var rip = context.HttpContext.Connection.RemoteIpAddress.ToString();

                if (!rip.Contains("127.0.0.1"))
                {
                    var timeStr = context.HttpContext.Request.Headers["Time"].ToString();
                    var time = Methods.DataTime.DateTimeHelper.JsToTime(long.Parse(timeStr));

                    if (time.AddMinutes(10) > DateTime.Now)
                    {
                        string privatekey = "gPmgRr9Dp3wzubTaGIgmMSpfNiKqkIAA0C8gkaBSN0ca3GWxk3W6682KuXRpxnDq";

                        string strdata = privatekey + timeStr;


                        if (context.HttpContext.Request.Method == "POST")
                        {
                            string body = Methods.Http.HttpContext.RequestBody;

                            if (!string.IsNullOrEmpty(body))
                            {
                                strdata = strdata + body;
                            }
                            else if (context.HttpContext.Request.HasFormContentType)
                            {
                                var fromlist = context.HttpContext.Request.Form.OrderBy(t => t.Key).ToList();

                                foreach (var fm in fromlist)
                                {
                                    strdata = strdata + fm.Key + fm.Value.ToString();
                                }
                            }
                        }
                        else if (context.HttpContext.Request.Method == "GET")
                        {
                            var queryList = context.HttpContext.Request.Query.ToList();

                            foreach (var query in queryList)
                            {
                                strdata = strdata + query.Key + query.Value;
                            }
                        }


                        string tk = Methods.Crypto.Md5.GetMd5(strdata).ToLower();

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

        }


        void IActionFilter.OnActionExecuted(ActionExecutedContext context)
        {

        }
    }
}
