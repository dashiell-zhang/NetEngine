using Application.Model.Pay;
using Common;
using DistributedLock;
using Microsoft.Extensions.Caching.Distributed;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace Application.Service.Payment.Clients;

/// <summary>
/// 封装微信支付协议、签名及平台证书，不访问业务数据库
/// </summary>
internal sealed class WeiXinPayClient(HttpClient httpClient, IDistributedCache cache, IDistributedLock distributedLock)
{

    /// <summary>
    /// 同一商户的平台证书下载最小间隔，失败尝试也计入限制
    /// </summary>
    private const int CertificateRefreshIntervalSeconds = 60;


    /// <summary>
    /// 创建JSAPI支付参数
    /// </summary>
    public async Task<CreateWeiXinPayJSAPIRetDto> CreateJSAPIAsync(WeiXinPayOptions options, string orderNo, decimal amount, string subject, string openId, string notifyUrl)
    {

        string prepayId = await CreateTransactionAsync(options, "jsapi", orderNo, amount, subject, notifyUrl, new { openid = openId });
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        string package = "prepay_id=" + prepayId;
        return new CreateWeiXinPayJSAPIRetDto
        {
            AppId = options.AppId,
            Package = package,
            NonceStr = nonce,
            TimeStamp = timestamp,
            Sign = Sign(options, $"{options.AppId}\n{timestamp}\n{nonce}\n{package}\n")
        };

    }


    /// <summary>
    /// 创建App支付参数
    /// </summary>
    public async Task<CreateWeiXinPayAppRetDto> CreateAppAsync(WeiXinPayOptions options, string orderNo, decimal amount, string subject, string notifyUrl)
    {

        string prepayId = await CreateTransactionAsync(options, "app", orderNo, amount, subject, notifyUrl);
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        return new CreateWeiXinPayAppRetDto
        {
            AppId = options.AppId,
            PartnerId = options.MerchantId,
            PrepayId = prepayId,
            NonceStr = nonce,
            TimeStamp = timestamp,
            Sign = Sign(options, $"{options.AppId}\n{timestamp}\n{nonce}\n{prepayId}\n")
        };

    }


    /// <summary>
    /// 创建H5支付地址
    /// </summary>
    public Task<string> CreateH5Async(WeiXinPayOptions options, string orderNo, decimal amount, string subject, string clientIP, string notifyUrl)
    {

        return CreateTransactionAsync(options, "h5", orderNo, amount, subject, notifyUrl, scene: new { payer_client_ip = clientIP, h5_info = new { type = "Wap" } });

    }


    /// <summary>
    /// 创建Native支付二维码地址
    /// </summary>
    public Task<string> CreatePCAsync(WeiXinPayOptions options, string orderNo, decimal amount, string subject, string notifyUrl)
    {

        return CreateTransactionAsync(options, "native", orderNo, amount, subject, notifyUrl);

    }


    /// <summary>
    /// 验签原始通知后解密支付结果并校验商户及应用归属
    /// </summary>
    public async Task<PaymentNotification> ReadNotificationAsync(WeiXinPayOptions options, Dictionary<string, string> headers, string body)
    {

        await VerifyAsync(options, headers, body);
        using var notification = JsonDocument.Parse(body);
        var root = notification.RootElement;
        if (root.GetProperty("event_type").GetString() != "TRANSACTION.SUCCESS" || root.GetProperty("resource_type").GetString() != "encrypt-resource")
        {
            throw new CustomException("微信通知不是支付成功通知");
        }

        var resource = root.GetProperty("resource");
        string json = Decrypt(options, resource);
        using var transaction = JsonDocument.Parse(json);
        var payment = transaction.RootElement;
        if (payment.GetProperty("mchid").GetString() != options.MerchantId || payment.GetProperty("appid").GetString() != options.AppId || payment.GetProperty("trade_state").GetString() != "SUCCESS")
        {
            throw new CustomException("微信通知商户、应用或交易状态不匹配");
        }

        var amount = payment.GetProperty("amount");
        string? orderNo = payment.GetProperty("out_trade_no").GetString();
        string? transactionId = payment.GetProperty("transaction_id").GetString();
        int total = amount.GetProperty("total").GetInt32();
        if (amount.GetProperty("currency").GetString() != "CNY" || total <= 0 || string.IsNullOrWhiteSpace(orderNo) || string.IsNullOrWhiteSpace(transactionId))
        {
            throw new CustomException("微信通知支付信息不完整");
        }

        return new PaymentNotification(orderNo, total / 100m, transactionId, payment.GetProperty("success_time").GetDateTimeOffset().ToUniversalTime());

    }


    /// <summary>
    /// 按交易场景构造下单报文并解析对应的平台结果
    /// </summary>
    private async Task<string> CreateTransactionAsync(WeiXinPayOptions options, string tradeType, string orderNo, decimal amount, string subject, string notifyUrl, object? payer = null, object? scene = null)
    {

        decimal cents = amount * 100;
        if (amount <= 0 || decimal.Truncate(cents) != cents || cents > int.MaxValue)
        {
            throw new CustomException("微信支付金额必须为有效的整数分");
        }

        var data = new Dictionary<string, object>
        {
            ["mchid"] = options.MerchantId,
            ["appid"] = options.AppId,
            ["out_trade_no"] = orderNo,
            ["description"] = subject,
            ["notify_url"] = notifyUrl,
            ["amount"] = new { total = (int)cents, currency = "CNY" }
        };
        if (payer is not null)
        {
            data["payer"] = payer;
        }
        if (scene is not null)
        {
            data["scene_info"] = scene;
        }

        using var response = await SendAsync(options, "/v3/pay/transactions/" + tradeType, data);
        string body = await response.Content.ReadAsStringAsync();
        await VerifyAsync(options, ReadHeaders(response), body);
        if (!response.IsSuccessStatusCode)
        {
            throw new CustomException("微信支付下单失败");
        }

        using var document = JsonDocument.Parse(body);
        string property = tradeType switch { "h5" => "h5_url", "native" => "code_url", _ => "prepay_id" };
        if (!document.RootElement.TryGetProperty(property, out var value) || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new CustomException("微信支付下单结果不完整");
        }

        return value.GetString()!;

    }


    /// <summary>
    /// 使用平台指定序列号的证书验证签名
    /// </summary>
    private async Task VerifyAsync(WeiXinPayOptions options, Dictionary<string, string> headers, string body)
    {

        var normalized = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
        string serial = ValidateSignatureHeaders(normalized);
        var certificates = await GetCertificatesAsync(options, serial);
        Verify(normalized, body, certificates);

    }


    /// <summary>
    /// 按商户缓存平台证书，未知序列号触发的下载受商户级刷新间隔限制
    /// </summary>
    private async Task<List<WeiXinPlatformCertificate>> GetCertificatesAsync(WeiXinPayOptions options, string serial)
    {

        string key = "Payment:WeiXin:Certificates:" + options.MerchantId;
        var certificates = await cache.GetAsync<List<WeiXinPlatformCertificate>>(key);
        if (certificates?.Any(item => string.Equals(item.Serial, serial, StringComparison.OrdinalIgnoreCase)) == true)
        {
            return certificates;
        }

        string refreshKey = key + ":refresh";
        if (await cache.GetStringAsync(refreshKey) is not null)
        {
            throw new CustomException("微信支付平台证书刷新过于频繁，请稍后重试");
        }

        await using var handle = await distributedLock.LockAsync(key + ":lock");
        certificates = await cache.GetAsync<List<WeiXinPlatformCertificate>>(key);
        if (certificates?.Any(item => string.Equals(item.Serial, serial, StringComparison.OrdinalIgnoreCase)) == true)
        {
            return certificates;
        }

        if (await cache.GetStringAsync(refreshKey) is not null)
        {
            throw new CustomException("微信支付平台证书刷新过于频繁，请稍后重试");
        }
        // 下载前记录尝试，防止未知序列号或下载失败反复触发外部请求
        await cache.SetStringAsync(refreshKey, "1", new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CertificateRefreshIntervalSeconds) });

        using var response = await SendAsync(options, "/v3/certificates");
        if (!response.IsSuccessStatusCode)
        {
            throw new CustomException("微信支付平台证书获取失败");
        }
        string body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        certificates = [];
        foreach (var item in document.RootElement.GetProperty("data").EnumerateArray())
        {
            string pem = Decrypt(options, item.GetProperty("encrypt_certificate"));
            string certificateSerial = item.GetProperty("serial_no").GetString()!;
            using var certificate = X509Certificate2.CreateFromPem(pem);
            if (!string.Equals(certificate.SerialNumber, certificateSerial, StringComparison.OrdinalIgnoreCase))
            {
                throw new CustomException("微信支付平台证书序列号不匹配");
            }
            certificates.Add(new WeiXinPlatformCertificate(certificateSerial, pem));
        }

        // 首次下载的证书先通过APIv3密钥的认证解密，再验证下载响应签名
        Verify(ReadHeaders(response), body, certificates);
        await cache.SetAsync(key, certificates, TimeSpan.FromHours(1));
        return certificates;

    }


    /// <summary>
    /// 校验签名头、时间戳及证书有效期并验证原始报文
    /// </summary>
    private static void Verify(Dictionary<string, string> headers, string body, List<WeiXinPlatformCertificate> certificates)
    {

        string serial = ValidateSignatureHeaders(headers);
        var item = certificates.FirstOrDefault(item => string.Equals(item.Serial, serial, StringComparison.OrdinalIgnoreCase)) ?? throw new CustomException("未找到微信支付验签证书");
        using var certificate = X509Certificate2.CreateFromPem(item.Pem);
        var now = DateTime.UtcNow;
        if (now < certificate.NotBefore.ToUniversalTime() || now > certificate.NotAfter.ToUniversalTime())
        {
            throw new CustomException("微信支付验签证书已失效");
        }
        using var rsa = certificate.GetRSAPublicKey();
        if (rsa is null || !rsa.VerifyData(Encoding.UTF8.GetBytes($"{headers["Wechatpay-Timestamp"]}\n{headers["Wechatpay-Nonce"]}\n{body}\n"), Convert.FromBase64String(headers["Wechatpay-Signature"]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
        {
            throw new CustomException("微信支付签名验证失败");
        }

    }


    /// <summary>
    /// 获取证书前校验必需签名头及时间戳，拒绝缺失、空白或过期的签名信息
    /// </summary>
    private static string ValidateSignatureHeaders(Dictionary<string, string> headers)
    {

        if (!headers.TryGetValue("Wechatpay-Serial", out var serial) || string.IsNullOrWhiteSpace(serial) || !headers.TryGetValue("Wechatpay-Timestamp", out var timestamp) || !headers.TryGetValue("Wechatpay-Nonce", out var nonce) || string.IsNullOrWhiteSpace(nonce) || !headers.TryGetValue("Wechatpay-Signature", out var signature) || string.IsNullOrWhiteSpace(signature) || !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || Math.Abs((decimal)DateTimeOffset.UtcNow.ToUnixTimeSeconds() - seconds) > 300)
        {
            throw new CustomException("微信支付签名信息不完整或已过期");
        }
        return serial;

    }


    /// <summary>
    /// 构造固定微信支付网关的签名请求
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(WeiXinPayOptions options, string path, object? data = null)
    {

        string body = data is null ? "" : JsonSerializer.Serialize(data);
        string method = data is null ? "GET" : "POST";
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        string signature = Sign(options, $"{method}\n{path}\n{timestamp}\n{nonce}\n{body}\n");
        using var request = new HttpRequestMessage(data is null ? HttpMethod.Get : HttpMethod.Post, "https://api.mch.weixin.qq.com" + path);
        if (data is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("User-Agent", "NetEngine-Payment");
        request.Headers.Add("Authorization", $"WECHATPAY2-SHA256-RSA2048 mchid=\"{options.MerchantId}\",nonce_str=\"{nonce}\",timestamp=\"{timestamp}\",serial_no=\"{options.CertificateSerial}\",signature=\"{signature}\"");
        return await httpClient.SendAsync(request);

    }


    /// <summary>
    /// 收集响应签名头并忽略HTTP头名称大小写
    /// </summary>
    private static Dictionary<string, string> ReadHeaders(HttpResponseMessage response)
    {

        return response.Headers.ToDictionary(item => item.Key, item => item.Value.First(), StringComparer.OrdinalIgnoreCase);

    }


    /// <summary>
    /// 使用商户私钥生成RSA2签名
    /// </summary>
    private static string Sign(WeiXinPayOptions options, string message)
    {

        return CryptoHelper.RSASignData(options.PrivateKey, message, "base64", HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

    }


    /// <summary>
    /// 使用APIv3密钥认证解密平台证书或通知资源
    /// </summary>
    private static string Decrypt(WeiXinPayOptions options, JsonElement resource)
    {

        if (resource.GetProperty("algorithm").GetString() != "AEAD_AES_256_GCM")
        {
            throw new CustomException("微信支付加密算法不支持");
        }
        string? associatedData = resource.TryGetProperty("associated_data", out var associated) ? associated.GetString() : null;
        return CryptoHelper.AesGcmDecrypt(resource.GetProperty("ciphertext").GetString()!, options.ApiV3Key, resource.GetProperty("nonce").GetString()!, associatedData, "base64");

    }

}
