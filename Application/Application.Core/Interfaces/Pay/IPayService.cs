using Application.Model.Pay.Pay;

namespace Application.Core.Interfaces.Pay
{
    public interface IPayService
    {

        /// <summary>
        /// 微信支付-JSAPI模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <param name="openId">用户OpenId</param>
        /// <param name="notifyUrl">异步回调Url</param>
        /// <returns></returns>
        Task<DtoCreateWeiXinPayJSAPIRet?> CreateWeiXinPayJSAPIAsync(string orderNo, string openId, string notifyUrl);


        /// <summary>
        /// 微信支付-App模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <param name="notifyUrl">异步回调Url</param>
        /// <returns></returns>
        Task<DtoCreateWeiXinPayAppRet?> CreateWeiXinPayAppAsync(string orderNo, string notifyUrl);


        /// <summary>
        /// 微信支付-H5模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <param name="notifyUrl">异步回调Url</param>
        /// <param name="clientIP">客户端ip</param>
        /// <returns></returns>
        Task<string?> CreateWeiXinPayH5Async(string orderNo, string notifyUrl, string clientIP);


        /// <summary>
        /// 微信支付-PC模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <param name="notifyUrl">异步回调Url</param>
        /// <returns></returns>
        Task<string?> CreateWeiXinPayPCAsync(string orderNo, string notifyUrl);


        /// <summary>
        /// 微信支付异步通知接口
        /// </summary>
        /// <param name="mchId">商户Id</param>
        /// <param name="weiXinPayNotify"></param>
        /// <param name="headers"HttpHeader></param>
        /// <param name="requestBody">bodyjson数据</param>
        /// <returns></returns>
        Task<DtoWeiXinPayNotifyRet?> WeiXinPayNotifyAsync(string mchId, DtoWeiXinPayNotify weiXinPayNotify, Dictionary<string, string> headers, string requestBody);


        /// <summary>
        /// 微信支付退款状态查询
        /// </summary>
        Task WeiXinPayRefundSelectAsync();


        /// <summary>
        /// 微信支付退款
        /// </summary>
        Task WeiXinPayRefundAsync();


        /// <summary>
        /// 支付宝支付-小程序模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns>TradeNo</returns>
        Task<string?> CreateAliPayMiniAppAsync(string orderNo, string notifyUrl);


        /// <summary>
        /// 支付宝支付-PC模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <param name="notifyUrl">异步返回Url</param>
        /// <param name="returnUrl">同步返回Url(二维码模式可为null)</param>
        /// <returns>支付宝支付Url</returns>
        Task<string?> CreateAliPayPCAsync(string orderNo, string notifyUrl, string? returnUrl);


        /// <summary>
        /// 支付宝支付-H5模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <param name="notifyUrl"></param>
        /// <param name="returnUrl"></param>
        /// <param name="quitUrl"></param>
        /// <returns>支付宝支付Url</returns>
        Task<string?> CreateAliPayH5Async(string orderNo, string notifyUrl, string returnUrl, string quitUrl);


        /// <summary>
        /// 支付宝异步通知接口
        /// </summary>
        /// <param name="parameters">支付宝回调表单参数</param>
        /// <returns></returns>
        Task<string?> AliPayNotifyAsync(Dictionary<string, string> parameters);


        /// <summary>
        /// 支付宝退款
        /// </summary>
        /// <returns></returns>
        Task AliPayRefundAsync();


        /// <summary>
        /// 支付宝退款查询接口
        /// </summary>
        /// <returns></returns>
        Task AliPayRefundSelectAsync();


        /// <summary>
        /// 微信支付Http请求
        /// </summary>
        /// <param name="mchId">商户Id</param>
        /// <param name="url">接口地址</param>
        /// <param name="data">请求数据，数据为空则认为是get请求</param>
        /// <returns></returns>
        Task<string> WeiXinPayHttpAsync(string mchId, string url, object? data = null);



        Task<DtoWeiXinPayCertificates> GetWeiXinPayCertificatesAsync(string mchId);


        bool VerifySign(Dictionary<string, string> headers, string body, DtoWeiXinPayCertificates weiXinPayCertificates);


    }
}
