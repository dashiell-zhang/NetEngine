using Client.Interface.Models.Pay;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebAPI.Core.Models.Shared;

namespace Client.Interface
{
    public interface IPayService
    {

        /// <summary>
        /// 微信支付-JSAPI模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <param name="openId">用户OpenId</param>
        /// <returns></returns>
        public DtoCreateWeiXinPayJSAPIRet? CreateWeiXinPayJSAPI(string orderNo, string openId);


        /// <summary>
        /// 微信支付-App模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns></returns>
        public DtoCreateWeiXinPayAppRet? CreateWeiXinPayApp(string orderNo);


        /// <summary>
        /// 微信支付-H5模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns></returns>
        public string? CreateWeiXinPayH5(string orderNo);



        /// <summary>
        /// 微信支付-PC模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns></returns>
        public string? CreateWeiXinPayPC(string orderNo);



        /// <summary>
        /// 微信支付异步通知接口
        /// </summary>
        public DtoWeiXinPayNotifyRet? WeiXinPayNotify(string mchId, DtoWeiXinPayNotify weiXinPayNotify);


        /// <summary>
        /// 微信支付退款状态查询
        /// </summary>
        public void WeiXinPayRefundSelect();


        /// <summary>
        /// 微信支付退款
        /// </summary>
        public void WeiXinPayRefund();


        /// <summary>
        /// 支付宝支付-小程序模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns></returns>
        public DtoKeyValue? CreateAliPayMiniApp(string orderNo);



        /// <summary>
        /// 支付宝支付-PC模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns>支付宝支付URL</returns>
        public string? CreateAliPayPC(string orderNo);



        /// <summary>
        /// 支付宝支付-H5模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns>支付宝支付URL</returns>
        public string? CreateAliPayH5(string orderNo);


        /// <summary>
        /// 支付宝异步通知接口
        /// </summary>
        /// <returns></returns>
        public string? AliPayNotify();



        /// <summary>
        /// 支付宝退款
        /// </summary>
        /// <returns></returns>
        public void AliPayRefund();



        /// <summary>
        /// 支付宝退款查询接口
        /// </summary>
        /// <returns></returns>
        public void AliPayRefundSelect();


        /// <summary>
        /// 微信支付Http请求
        /// </summary>
        /// <param name="mchId">商户Id</param>
        /// <param name="url">接口地址</param>
        /// <param name="data">请求数据，数据为空则认为是get请求</param>
        /// <returns></returns>
        public string WeiXinPayHttp(string mchId, string url, object? data = null);



        public DtoWeiXinPayCertificates GetWeiXinPayCertificates(string mchId);




        public bool VerifySign(Dictionary<string, string> headers, string body, DtoWeiXinPayCertificates weiXinPayCertificates);





    }
}
