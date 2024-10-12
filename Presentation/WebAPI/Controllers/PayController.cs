using Microsoft.AspNetCore.Mvc;
using Web.Interface;
using Web.Interface.Models.Pay;
using WebAPIBasic.Models.Shared;

namespace WebAPI.Controllers
{

    /// <summary>
    /// 第三方支付发起集合，依赖于订单号
    /// </summary>
    [Route("[controller]/[action]")]
    [ApiController]
    public class PayController(IPayService payService) : ControllerBase
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
            return payService.CreateWeiXinPayJSAPI(orderNo, openId);
        }



        /// <summary>
        /// 微信支付-App模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns></returns>
        [HttpGet]
        public DtoCreateWeiXinPayAppRet? CreateWeiXinPayApp(string orderNo)
        {
            return payService.CreateWeiXinPayApp(orderNo);
        }



        /// <summary>
        /// 微信支付-H5模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns></returns>
        [HttpGet]
        public string? CreateWeiXinPayH5(string orderNo)
        {
            return payService.CreateWeiXinPayH5(orderNo);
        }



        /// <summary>
        /// 微信支付-PC模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns></returns>
        [HttpGet]
        public string? CreateWeiXinPayPC(string orderNo)
        {
            return payService.CreateWeiXinPayPC(orderNo);
        }



        /// <summary>
        /// 微信支付异步通知接口
        /// </summary>
        [HttpPost("{mchid}")]
        public DtoWeiXinPayNotifyRet? WeiXinPayNotify(string mchId, DtoWeiXinPayNotify weiXinPayNotify)
        {
            return payService.WeiXinPayNotify(mchId, weiXinPayNotify);
        }



        /// <summary>
        /// 支付宝支付-小程序模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns></returns>
        [HttpGet]
        public DtoKeyValue? CreateAliPayMiniApp(string orderNo)
        {
            return payService.CreateAliPayMiniApp(orderNo);
        }




        /// <summary>
        /// 支付宝支付-PC模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns>支付宝支付URL</returns>
        [HttpGet]
        public string? CreateAliPayPC(string orderNo)
        {
            return payService.CreateAliPayPC(orderNo);
        }



        /// <summary>
        /// 支付宝支付-H5模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns>支付宝支付URL</returns>
        [HttpGet]
        public string? CreateAliPayH5(string orderNo)
        {
            return payService.CreateAliPayH5(orderNo);
        }



        /// <summary>
        /// 支付宝异步通知接口
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public string? AliPayNotify()
        {
            return payService.AliPayNotify();
        }

    }
}