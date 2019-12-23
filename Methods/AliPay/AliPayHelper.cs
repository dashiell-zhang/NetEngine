using Aop.Api;
using Aop.Api.Domain;
using Aop.Api.Request;
using Aop.Api.Response;
using System;

namespace Methods.AliPay
{
    public class AliPayHelper
    {

        /// <summary>
        /// AppId
        /// </summary>
        public string appid;


        /// <summary>
        /// 应用私钥
        /// </summary>
        public string appprivatekey;


        /// <summary>
        /// 支付宝公钥
        /// </summary>
        public string alipaypublickey;


        /// <summary>
        /// 支付回调 同步URL
        /// </summary>
        public string returnUrl;


        /// <summary>
        /// 支付回调 异步URL
        /// </summary>
        public string notifyUrl;


        /// <summary>
        /// 支付中途退出后跳转URL
        /// </summary>
        public string quitUrl;


        /// <param name="in_appid">AppId</param>
        /// <param name="in_appprivatekey">应用私钥</param>
        /// <param name="in_alipaypublickey">支付宝公钥</param>
        /// <param name="in_returnUrl">支付回调 同步URL</param>
        /// <param name="in_notifyUrl">支付回调 异步URL</param>
        /// <param name="in_quitUrl">支付中途退出后跳转URL</param>
        /// <remarks>H5 支付初始化</remarks>
        public AliPayHelper(string in_appid, string in_appprivatekey, string in_alipaypublickey, string in_returnUrl, string in_notifyUrl, string in_quitUrl)
        {
            appid = in_appid;
            appprivatekey = in_appprivatekey;
            alipaypublickey = in_alipaypublickey;
            returnUrl = in_returnUrl;
            notifyUrl = in_notifyUrl;
            quitUrl = in_quitUrl;
        }



        /// <param name="in_appid">AppId</param>
        /// <param name="in_appprivatekey">应用私钥</param>
        /// <param name="in_alipaypublickey">支付宝公钥</param>
        /// <param name="in_notifyUrl">支付回调 异步URL</param>
        /// <remarks>APP支付初始化</remarks>
        public AliPayHelper(string in_appid, string in_appprivatekey, string in_alipaypublickey, string in_notifyUrl)
        {
            appid = in_appid;
            appprivatekey = in_appprivatekey;
            alipaypublickey = in_alipaypublickey;
            notifyUrl = in_notifyUrl;
        }



        /// <summary>
        /// 创建支付宝商户订单_H5
        /// </summary>
        /// <param name="orderno">商户订单号</param>
        /// <param name="title">商品名称</param>
        /// <param name="price">商品价格</param>
        /// <param name="describe">商品描述</param>
        /// <returns></returns>
        public string CreatePayH5(string orderno, string title, string price, string describe)
        {
            // 支付宝网关
            string gatewayUrl = "https://openapi.alipay.com/gateway.do";

            DefaultAopClient client = new DefaultAopClient(gatewayUrl, appid, appprivatekey, "json", "1.0", "RSA2", alipaypublickey, "UTF-8", false);

            // 组装业务参数model
            AlipayTradeWapPayModel model = new AlipayTradeWapPayModel
            {
                Body = describe,    // 商品描述

                Subject = title,    // 订单名称

                TotalAmount = price,    // 付款金额

                OutTradeNo = orderno,   // 外部订单号，商户网站订单系统中唯一的订单号

                ProductCode = "QUICK_WAP_WAY",

                QuitUrl = quitUrl   // 支付中途退出返回商户网站地址
            };

            AlipayTradeWapPayRequest request = new AlipayTradeWapPayRequest();

            // 设置支付完成同步回调地址
            request.SetReturnUrl(returnUrl);

            // 设置支付完成异步通知接收地址
            request.SetNotifyUrl(notifyUrl);

            // 将业务model载入到request
            request.SetBizModel(model);

            AlipayTradeWapPayResponse response = null;

            try
            {

                //调用 SDK 集成方法构造HTML表单代码
                response = client.pageExecute(request, null, "post");
                return response.Body;
            }
            catch (Exception ex)
            {
                return "";
            }
        }





        /// <summary>
        /// 统一收单交易创建接口
        /// </summary>
        /// <param name="orderNo">商户订单号</param>
        /// <param name="title">商品名称</param>
        /// <param name="price">商品价格</param>
        /// <param name="buyerId">购买人支付宝用户ID</param>
        /// <returns></returns>
        public string AlipayTradeCreate(string orderNo, string title, string price, string buyerId)
        {
            IAopClient client = new DefaultAopClient("https://openapi.alipay.com/gateway.do", appid, appprivatekey, "json", "1.0", "RSA2", alipaypublickey, "utf-8", false);

            AlipayTradeCreateRequest request = new AlipayTradeCreateRequest();


            AlipayTradeCreateModel model = new AlipayTradeCreateModel
            {
                TotalAmount = price,
                Subject = title,
                OutTradeNo = orderNo,
                BuyerId = buyerId
            };

            request.SetBizModel(model);

            request.SetNotifyUrl(notifyUrl);

            AlipayTradeCreateResponse response = client.Execute(request);


            return response.TradeNo;
        }
    }
}
