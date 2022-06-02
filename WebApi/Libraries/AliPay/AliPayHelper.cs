using Aop.Api;
using Aop.Api.Domain;
using Aop.Api.Request;
using Aop.Api.Response;
using System;

namespace WebApi.Libraries.AliPay
{
    public class AliPayHelper
    {

        /// <summary>
        /// AppId
        /// </summary>
        private readonly string appid;


        /// <summary>
        /// 应用私钥
        /// </summary>
        private readonly string appprivatekey;


        /// <summary>
        /// 支付宝公钥
        /// </summary>
        private readonly string alipaypublickey;


        /// <summary>
        /// 支付回调 同步URL
        /// </summary>
        private readonly string? returnUrl;


        /// <summary>
        /// 支付回调 异步URL
        /// </summary>
        private readonly string notifyUrl;



        /// <summary>
        /// 支付中途退出后跳转URL
        /// </summary>
        private readonly string? quitUrl;


        /// <param name="appId">AppId</param>
        /// <param name="privateKey">应用私钥</param>
        /// <param name="publicKey">支付宝公钥</param>
        /// <param name="notifyUrl">支付回调 异步URL</param>
        /// <param name="returnUrl">支付回调 同步URL</param>
        /// <param name="quitUrl">支付中途退出后跳转URL</param>
        /// <remarks>H5 支付初始化</remarks>
        public AliPayHelper(string appId, string privateKey, string publicKey, string notifyUrl, string returnUrl, string quitUrl)
        {
            appid = appId;
            appprivatekey = privateKey;
            alipaypublickey = publicKey;
            this.returnUrl = returnUrl;
            this.notifyUrl = notifyUrl;
            this.quitUrl = quitUrl;
        }



        /// <param name="appId">AppId</param>
        /// <param name="privateKey">应用私钥</param>
        /// <param name="publicKey">支付宝公钥</param>
        /// <param name="notifyUrl">支付回调 异步URL</param>
        /// <remarks>APP支付初始化</remarks>
        public AliPayHelper(string appId, string privateKey, string publicKey, string notifyUrl)
        {
            appid = appId;
            appprivatekey = privateKey;
            alipaypublickey = publicKey;
            this.notifyUrl = notifyUrl;
        }



        /// <summary>
        /// 创建支付宝商户订单_H5
        /// </summary>
        /// <param name="orderNo">商户订单号</param>
        /// <param name="title">商品名称</param>
        /// <param name="price">商品价格</param>
        /// <param name="describe">商品描述</param>
        /// <returns></returns>
        public string CreatePayH5(string orderNo, string title, string price, string describe)
        {
            // 支付宝网关
            string gatewayUrl = "https://openapi.alipay.com/gateway.do";

            DefaultAopClient client = new(gatewayUrl, appid, appprivatekey, "json", "1.0", "RSA2", alipaypublickey, "UTF-8", false);

            // 组装业务参数model
            AlipayTradeWapPayModel model = new()
            {
                OutTradeNo = orderNo,
                Subject = title,
                TotalAmount = price,
                Body = describe,
                ProductCode = "QUICK_WAP_WAY",
                QuitUrl = quitUrl
            };

            AlipayTradeWapPayRequest request = new();

            // 设置支付完成同步回调地址
            request.SetReturnUrl(returnUrl);

            // 设置支付完成异步通知接收地址
            request.SetNotifyUrl(notifyUrl);

            // 将业务model载入到request
            request.SetBizModel(model);


            //调用 SDK 集成方法构造HTML表单代码
            var response = client.pageExecute(request, null, "post");
            return response.Body;
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

            AlipayTradeCreateRequest request = new();


            AlipayTradeCreateModel model = new()
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



        /// <param name="appId">AppId</param>
        /// <param name="privateKey">应用私钥</param>
        /// <param name="publicKey">支付宝公钥</param>
        /// <param name="notifyUrl">支付回调 异步URL</param>
        /// <param name="returnUrl">支付回调 同步URL</param>
        /// <remarks>WEB网站支付初始化</remarks>
        public AliPayHelper(string appId, string privateKey, string publicKey, string notifyUrl, string returnUrl)
        {
            appid = appId;
            appprivatekey = privateKey;
            alipaypublickey = publicKey;
            this.notifyUrl = notifyUrl;
            this.returnUrl = returnUrl;
        }



        /// <summary>
        /// PC网页支付创建方法
        /// </summary>
        /// <param name="body">订单描述</param>
        /// <param name="subject">订单名称</param>
        /// <param name="totalAmout">订单金额</param>
        /// <param name="tradeNo">商户订单号</param>
        /// <returns></returns>
        public string CreatePayPC(string body, string subject, string totalAmout, string tradeNo)
        {
            DefaultAopClient client = new("https://openapi.alipay.com/gateway.do", appid, appprivatekey, "json", "2.0", "RSA2", alipaypublickey, "UTF-8", false);

            // 组装业务参数model
            AlipayTradePagePayModel model = new()
            {
                Body = body,
                Subject = subject,
                TotalAmount = totalAmout,
                OutTradeNo = tradeNo,
                ProductCode = "FAST_INSTANT_TRADE_PAY"
            };

            AlipayTradePagePayRequest request = new();
            // 设置同步回调地址
            request.SetReturnUrl(returnUrl);
            // 设置异步通知接收地址
            request.SetNotifyUrl(notifyUrl);
            // 将业务model载入到request
            request.SetBizModel(model);

            var response = client.SdkExecute(request);
            Console.WriteLine($"订单支付发起成功，订单号：{tradeNo}");

            //跳转支付宝支付
            string url = "https://openapi.alipay.com/gateway.do" + "?" + response.Body;

            return url;
        }
    }
}
