using Application.Model.Pay;

namespace Application.Interface;

/// <summary>
/// 支付请求创建与支付回调处理
/// </summary>
public interface IPayService
{

    /// <summary>
    /// 微信支付-JSAPI模式
    /// </summary>
    Task<CreateWeiXinPayJSAPIRetDto?> CreateWeiXinPayJSAPIAsync(string orderNo, string openId, string notifyUrl);


    /// <summary>
    /// 微信支付-App模式
    /// </summary>
    Task<CreateWeiXinPayAppRetDto?> CreateWeiXinPayAppAsync(string orderNo, string notifyUrl);


    /// <summary>
    /// 微信支付-H5模式
    /// </summary>
    Task<string?> CreateWeiXinPayH5Async(string orderNo, string notifyUrl, string clientIP);


    /// <summary>
    /// 微信支付-PC模式
    /// </summary>
    Task<string?> CreateWeiXinPayPCAsync(string orderNo, string notifyUrl);


    /// <summary>
    /// 微信支付异步通知接口
    /// </summary>
    Task<WeiXinPayNotifyRetDto?> WeiXinPayNotifyAsync(string mchId, WeiXinPayNotifyDto weiXinPayNotify, Dictionary<string, string> headers, string requestBody);


    /// <summary>
    /// 支付宝支付-小程序模式
    /// </summary>
    Task<string?> CreateAliPayMiniAppAsync(string orderNo, string notifyUrl);


    /// <summary>
    /// 支付宝支付-PC模式
    /// </summary>
    Task<string?> CreateAliPayPCAsync(string orderNo, string notifyUrl, string? returnUrl);


    /// <summary>
    /// 支付宝支付-H5模式
    /// </summary>
    Task<string?> CreateAliPayH5Async(string orderNo, string notifyUrl, string returnUrl, string quitUrl);


    /// <summary>
    /// 支付宝异步通知接口
    /// </summary>
    Task<string?> AliPayNotifyAsync(Dictionary<string, string> parameters);

}
