using Microsoft.AspNetCore.Mvc;
using Pay.Interface;
using Pay.Model.Pay;
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
        public Task<DtoCreateWeiXinPayJSAPIRet?>  CreateWeiXinPayJSAPI(string orderNo, string openId)
        {
            var notifyUrl = httpContextAccessor.HttpContext!.GetBaseUrl() + "/Pay/WeiXinPayNotify/";

            return payService.CreateWeiXinPayJSAPIAsync(orderNo, openId, notifyUrl);
        }



        /// <summary>
        /// 微信支付-App模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns></returns>
        [HttpGet]
        public Task<DtoCreateWeiXinPayAppRet?>  CreateWeiXinPayApp(string orderNo)
        {

            var notifyUrl = httpContextAccessor.HttpContext!.GetBaseUrl() + "/Pay/WeiXinPayNotify/";

            return payService.CreateWeiXinPayAppAsync(orderNo, notifyUrl);
        }



        /// <summary>
        /// 微信支付-H5模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns></returns>
        [HttpGet]
        public Task<string?>  CreateWeiXinPayH5(string orderNo)
        {
            var notifyUrl = httpContextAccessor.HttpContext!.GetBaseUrl() + "/Pay/WeiXinPayNotify/";

            var clientIP = httpContextAccessor.HttpContext!.GetRemoteIP();

            return payService.CreateWeiXinPayH5Async(orderNo, notifyUrl, clientIP);
        }



        /// <summary>
        /// 微信支付-PC模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns></returns>
        [HttpGet]
        public Task<string?>  CreateWeiXinPayPC(string orderNo)
        {
            var notifyUrl = httpContextAccessor.HttpContext!.GetBaseUrl() + "/Pay/WeiXinPayNotify/";

            return payService.CreateWeiXinPayPCAsync(orderNo, notifyUrl);
        }



        /// <summary>
        /// 微信支付异步通知接口
        /// </summary>
        [HttpPost("{mchid}")]
        public async Task<DtoWeiXinPayNotifyRet?>  WeiXinPayNotify(string mchId, DtoWeiXinPayNotify weiXinPayNotify)
        {
            string requestBody = HttpContext.GetRequestBody();

            var headers = HttpContext.Request.Headers.ToDictionary(t => t.Key, t => t.Value.ToString());

            var ret = await payService.WeiXinPayNotifyAsync(mchId, weiXinPayNotify, headers, requestBody);

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
        public Task<string?>  CreateAliPayMiniApp(string orderNo)
        {
            var notifyUrl = httpContextAccessor.HttpContext!.GetBaseUrl() + "/api/Pay/AliPayNotify";

            return payService.CreateAliPayMiniAppAsync(orderNo, notifyUrl);
        }




        /// <summary>
        /// 支付宝支付-PC模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns>支付宝支付Url</returns>
        [HttpGet]
        public Task<string?>  CreateAliPayPC(string orderNo)
        {

            //var returnUrl = httpContextAccessor.HttpContext!.GetBaseUrl();
            var notifyUrl = httpContextAccessor.HttpContext!.GetBaseUrl() + "/Pay/AliPayNotify";

            return payService.CreateAliPayPCAsync(orderNo, notifyUrl, null);
        }



        /// <summary>
        /// 支付宝支付-H5模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns>支付宝支付Url</returns>
        [HttpGet]
        public Task<string?>  CreateAliPayH5(string orderNo)
        {
            var returnUrl = "";

            var notifyUrl = httpContextAccessor.HttpContext!.GetBaseUrl() + "/Pay/AliPayNotify";

            var quitUrl = "";

            return payService.CreateAliPayH5Async(orderNo, notifyUrl, returnUrl, quitUrl);
        }



        /// <summary>
        /// 支付宝异步通知接口
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public Task<string?>  AliPayNotify()
        {
            var parameters = HttpContext.Request.Form.ToDictionary(t => t.Key, t => t.Value.ToString());

            return payService.AliPayNotifyAsync(parameters);
        }

    }
}