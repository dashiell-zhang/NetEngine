using Blazored.LocalStorage;
using SourceGenerator.Runtime.Attributes;
using System.Security.Cryptography;
using System.Text;

namespace Admin.App.Services;

/// <summary>
/// 负责生成上传请求的鉴权数据
/// </summary>
[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class UploadSignatureService(ISyncLocalStorageService localStorage)
{

    private readonly ISyncLocalStorageService _localStorage = localStorage;


    /// <summary>
    /// 创建上传请求头数据
    /// </summary>
    public UploadSignatureResult CreateUploadSignature(string requestPath, byte[] fileBytes)
    {

        ArgumentException.ThrowIfNullOrWhiteSpace(requestPath);
        ArgumentNullException.ThrowIfNull(fileBytes);

        var authorization = _localStorage.GetItemAsString("Authorization");
        var timeText = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var fileSign = ComputeSha256Hex(fileBytes);
        var privateKey = authorization?.Split('.').LastOrDefault();
        var signText = privateKey + timeText + requestPath + "file" + fileSign;

        return new UploadSignatureResult
        {
            Authorization = authorization,
            TimeText = timeText,
            Token = ComputeSha256Hex(Encoding.UTF8.GetBytes(signText))
        };
    }


    /// <summary>
    /// 计算 SHA256 十六进制字符串
    /// </summary>
    private static string ComputeSha256Hex(byte[] content)
    {

        using var sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(content));
    }
}


/// <summary>
/// 表示上传签名结果
/// </summary>
public class UploadSignatureResult
{

    /// <summary>
    /// 授权令牌
    /// </summary>
    public string? Authorization { get; set; }


    /// <summary>
    /// 时间戳文本
    /// </summary>
    public string TimeText { get; set; } = string.Empty;


    /// <summary>
    /// 鉴权令牌
    /// </summary>
    public string Token { get; set; } = string.Empty;
}
