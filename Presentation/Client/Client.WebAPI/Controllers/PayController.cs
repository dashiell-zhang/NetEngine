using Client.Interface;
using Client.Interface.Models.Pay;
using Microsoft.AspNetCore.Mvc;
using Shared.Model;
using WebAPI.Core.Libraries;

namespace Client.WebAPI.Controllers
{

    /// <summary>
    /// 第三方支付发起集合，依赖于订单号
    /// </summary>
    [Route("[controller]/[action]")]
    [ApiController]
    public class PayController(IPayService payService, IHttpContextAccessor httpContextAccessor) : ControllerBase
    {


        /// <summary>
        /// 微信支付-JSAPI模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <param name="openId">用户OpenId</param>
        /// <returns></returns>
        [HttpGet]
        public DtoCreateWeiXinPayJSAPIRet? CreateWeiXinPayJSAPI(string orderNo, string openId)
        {
            var notifyUrl = httpContextAccessor.HttpContext!.GetBaseURL() + "/Pay/WeiXinPayNotify/";

            return payService.CreateWeiXinPayJSAPI(orderNo, openId, notifyUrl);
        }



        /// <summary>
        /// 微信支付-App模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns></returns>
        [HttpGet]
        public DtoCreateWeiXinPayAppRet? CreateWeiXinPayApp(string orderNo)
        {

            var notifyUrl = httpContextAccessor.HttpContext!.GetBaseURL() + "/Pay/WeiXinPayNotify/";

            return payService.CreateWeiXinPayApp(orderNo, notifyUrl);
        }



        /// <summary>
        /// 微信支付-H5模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns></returns>
        [HttpGet]
        public string? CreateWeiXinPayH5(string orderNo)
        {
            var notifyUrl = httpContextAccessor.HttpContext!.GetBaseURL() + "/Pay/WeiXinPayNotify/";

            var clientIP = httpContextAccessor.HttpContext!.GetRemoteIP();

            return payService.CreateWeiXinPayH5(orderNo, notifyUrl, clientIP);
        }



        /// <summary>
        /// 微信支付-PC模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns></returns>
        [HttpGet]
        public string? CreateWeiXinPayPC(string orderNo)
        {
            var notifyUrl = httpContextAccessor.HttpContext!.GetBaseURL() + "/Pay/WeiXinPayNotify/";

            return payService.CreateWeiXinPayPC(orderNo, notifyUrl);
        }



        /// <summary>
        /// 微信支付异步通知接口
        /// </summary>
        [HttpPost("{mchid}")]
        public DtoWeiXinPayNotifyRet? WeiXinPayNotify(string mchId, DtoWeiXinPayNotify weiXinPayNotify)
        {
            string requestBody = HttpContext.GetRequestBody();

            var headers = HttpContext.Request.Headers.ToDictionary(t => t.Key, t => t.Value.ToString());

            var ret = payService.WeiXinPayNotify(mchId, weiXinPayNotify, headers, requestBody);

            if (ret == null || ret.code != "SUCCESS")
            {
                HttpContext.Response.StatusCode = 501;
            }

            return ret;
        }



        /// <summary>
        /// 支付宝支付-小程序模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns></returns>
        [HttpGet]
        public DtoKeyValue? CreateAliPayMiniApp(string orderNo)
        {
            var notifyUrl = httpContextAccessor.HttpContext!.GetBaseURL() + "/api/Pay/AliPayNotify";

            return payService.CreateAliPayMiniApp(orderNo, notifyUrl);
        }




        /// <summary>
        /// 支付宝支付-PC模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns>支付宝支付URL</returns>
        [HttpGet]
        public string? CreateAliPayPC(string orderNo)
        {

            //var returnUrl = httpContextAccessor.HttpContext!.GetBaseURL();
            var notifyUrl = httpContextAccessor.HttpContext!.GetBaseURL() + "/Pay/AliPayNotify";

            return payService.CreateAliPayPC(orderNo, notifyUrl, null);
        }



        /// <summary>
        /// 支付宝支付-H5模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns>支付宝支付URL</returns>
        [HttpGet]
        public string? CreateAliPayH5(string orderNo)
        {
            var returnUrl = "";

            var notifyUrl = httpContextAccessor.HttpContext!.GetBaseURL() + "/Pay/AliPayNotify";

            var quitUrl = "";

            return payService.CreateAliPayH5(orderNo, notifyUrl, returnUrl, quitUrl);
        }



        /// <summary>
        /// 支付宝异步通知接口
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public string? AliPayNotify()
        {
            var parameters = HttpContext.Request.Form.ToDictionary(t => t.Key, t => t.Value.ToString());

            return payService.AliPayNotify(parameters);
        }

    }
}