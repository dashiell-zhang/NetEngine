namespace Application.Service.Payment.Clients;

/// <summary>
/// 支付宝接入配置，仅在支付模块内使用
/// </summary>
/// <param name="AppId">应用ID</param>
/// <param name="PrivateKey">应用私钥</param>
/// <param name="PublicKey">支付宝公钥</param>
internal sealed record AliPayOptions(string AppId, string PrivateKey, string PublicKey);


/// <summary>
/// 微信支付接入配置，仅在支付模块内使用
/// </summary>
/// <param name="AppId">应用ID</param>
/// <param name="MerchantId">商户号</param>
/// <param name="CertificateSerial">商户API证书序列号</param>
/// <param name="PrivateKey">商户API私钥</param>
/// <param name="ApiV3Key">通知及平台证书解密密钥</param>
internal sealed record WeiXinPayOptions(string AppId, string MerchantId, string CertificateSerial, string PrivateKey, string ApiV3Key);


/// <summary>
/// 通过验签及协议解析后的支付成功信息
/// </summary>
/// <param name="OrderNo">商户订单号</param>
/// <param name="Amount">订单金额，单位为元</param>
/// <param name="TransactionId">支付平台流水号</param>
/// <param name="PaidAt">支付成功时间</param>
internal sealed record PaymentNotification(string OrderNo, decimal Amount, string TransactionId, DateTimeOffset PaidAt);


/// <summary>
/// 已解密的微信支付平台证书
/// </summary>
/// <param name="Serial">平台证书序列号</param>
/// <param name="Pem">PEM格式证书</param>
internal sealed record WeiXinPlatformCertificate(string Serial, string Pem);
