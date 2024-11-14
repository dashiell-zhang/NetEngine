using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Cryptography;
using WebAPI.Core.Libraries;

namespace WebAPI.Core.Filters
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

#if !DEBUG
            var filter = (SignVerifyFilter)context.Filters.Where(t => t.ToString() == typeof(SignVerifyFilter).Assembly.GetName().Name + ".Filters.SignVerifyFilter").ToList().LastOrDefault()!;

            if (!filter.IsSkip)
            {
                var token = context.HttpContext.Request.Headers["Token"].ToString();

                var timeStr = context.HttpContext.Request.Headers["Time"].ToString();
                var time = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(timeStr));

                if (time.AddMinutes(3) > DateTime.UtcNow)
                {

                    var jsonWebToken = context.HttpContext.GetJsonWebToken();

                    string privateKey = jsonWebToken.EncodedSignature.ToString();

                    string dataStr = privateKey + timeStr;

                    var requestUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;

                    dataStr += requestUrl;

                    if (!context.HttpContext.Request.HasFormContentType)
                    {
                        string body = context.HttpContext.GetRequestBody();
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
                            using var fileStream = file.OpenReadStream();
                            using var sha256 = SHA256.Create();

                            var fileSign = Convert.ToHexString(sha256.ComputeHash(fileStream));

                            dataStr = dataStr + file.Name + fileSign;
                        }
                    }

                    string tk = Common.CryptoHelper.SHA256HashData(dataStr);

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
#endif
        }


        void IActionFilter.OnActionExecuted(ActionExecutedContext context)
        {

        }
    }
}
