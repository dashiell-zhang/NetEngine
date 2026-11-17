using Aop.Api;
using Aop.Api.Domain;
using Aop.Api.Request;
using Aop.Api.Util;
using Common;
using System.Globalization;

namespace Application.Service.Payment.Clients;

/// <summary>
/// 封装支付宝下单及通知验签，不访问业务数据库
/// </summary>
internal sealed class AliPayClient
{

    /// <summary>
    /// 支付宝开放平台网关
    /// </summary>
    private const string Gateway = "https://openapi.alipay.com/gateway.do";


    /// <summary>
    /// 创建支付宝小程序交易并返回平台交易号
    /// </summary>
    public string CreateMiniApp(AliPayOptions options, string orderNo, decimal amount, string subject, string buyerId, string notifyUrl)
    {

        var client = CreateClient(options);
        var request = new AlipayTradeCreateRequest();
        request.SetBizModel(new AlipayTradeCreateModel
        {
            TotalAmount = amount.ToString("0.00", CultureInfo.InvariantCulture),
            Subject = subject,
            OutTradeNo = orderNo,
            BuyerId = buyerId
        });
        request.SetNotifyUrl(notifyUrl);
        var response = client.Execute(request);
        if (response.IsError || string.IsNullOrWhiteSpace(response.TradeNo))
        {
            throw new CustomException("支付宝交易订单创建失败");
        }

        return response.TradeNo;

    }


    /// <summary>
    /// 创建支付宝PC支付地址
    /// </summary>
    public string CreatePC(AliPayOptions options, string orderNo, decimal amount, string subject, string notifyUrl, string? returnUrl)
    {

        var client = CreateClient(options);
        var request = new AlipayTradePagePayRequest();
        request.SetBizModel(new AlipayTradePagePayModel
        {
            Subject = subject,
            TotalAmount = amount.ToString("0.00", CultureInfo.InvariantCulture),
            OutTradeNo = orderNo,
            ProductCode = "FAST_INSTANT_TRADE_PAY",
            QrPayMode = "4"
        });
        request.SetNotifyUrl(notifyUrl);
        request.SetReturnUrl(returnUrl);
        return Gateway + "?" + client.SdkExecute(request).Body;

    }


    /// <summary>
    /// 创建支付宝H5支付地址
    /// </summary>
    public string CreateH5(AliPayOptions options, string orderNo, decimal amount, string subject, string notifyUrl, string returnUrl, string quitUrl)
    {

        var client = CreateClient(options);
        var request = new AlipayTradeWapPayRequest();
        request.SetBizModel(new AlipayTradeWapPayModel
        {
            Subject = subject,
            TotalAmount = amount.ToString("0.00", CultureInfo.InvariantCulture),
            OutTradeNo = orderNo,
            ProductCode = "QUICK_WAP_WAY",
            QuitUrl = quitUrl
        });
        request.SetNotifyUrl(notifyUrl);
        request.SetReturnUrl(returnUrl);
        return Gateway + "?" + client.SdkExecute(request).Body;

    }


    /// <summary>
    /// 验签后解析支付成功通知，校验应用归属并按北京时间解析付款时间
    /// </summary>
    public PaymentNotification ReadNotification(AliPayOptions options, Dictionary<string, string> parameters)
    {

        var signedParameters = new Dictionary<string, string>(parameters, StringComparer.Ordinal);
        if (parameters.GetValueOrDefault("app_id") != options.AppId || parameters.GetValueOrDefault("sign_type") != "RSA2" || !AlipaySignature.RSACheckV1(signedParameters, options.PublicKey, "utf-8", "RSA2", false))
        {
            throw new CustomException("支付宝通知签名或应用校验失败");
        }

        if (parameters.GetValueOrDefault("trade_status") is not ("TRADE_SUCCESS" or "TRADE_FINISHED"))
        {
            throw new CustomException("支付宝通知不是支付成功状态");
        }

        string? orderNo = parameters.GetValueOrDefault("out_trade_no");
        string? transactionId = parameters.GetValueOrDefault("trade_no");
        if (string.IsNullOrWhiteSpace(orderNo) || string.IsNullOrWhiteSpace(transactionId) || !decimal.TryParse(parameters.GetValueOrDefault("total_amount"), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) || amount <= 0 || !DateTime.TryParseExact(parameters.GetValueOrDefault("gmt_payment"), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var paidAt))
        {
            throw new CustomException("支付宝通知支付信息不完整");
        }

        return new PaymentNotification(orderNo, amount, transactionId, new DateTimeOffset(DateTime.SpecifyKind(paidAt, DateTimeKind.Unspecified), TimeSpan.FromHours(8)).ToUniversalTime());

    }


    /// <summary>
    /// 使用当前应用配置创建SDK客户端
    /// </summary>
    private static DefaultAopClient CreateClient(AliPayOptions options)
    {

        return new DefaultAopClient(Gateway, options.AppId, options.PrivateKey, "json", "1.0", "RSA2", options.PublicKey, "utf-8", false);

    }

}
