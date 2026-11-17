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
    /// 验证微信原始通知并确认订单支付，返回是否处理成功
    /// </summary>
    Task<bool> WeiXinPayNotifyAsync(string mchId, Dictionary<string, string> headers, string requestBody);


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
    /// 验证支付宝表单通知并确认订单支付，返回是否处理成功
    /// </summary>
    Task<bool> AliPayNotifyAsync(Dictionary<string, string> parameters);

}
