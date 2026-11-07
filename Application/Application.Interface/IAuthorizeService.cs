using Application.Model.Authorize;

namespace Application.Interface;

/// <summary>
/// 认证、令牌签发与功能权限校验
/// </summary>
public interface IAuthorizeService
{

    /// <summary>
    /// 获取公钥
    /// </summary>
    string? GetPublicKey();


    /// <summary>
    /// 获取Token认证信息
    /// </summary>
    Task<string?> GetTokenAsync(GetTokenDto login);


    /// <summary>
    /// 通过微信小程序Code获取Token认证信息
    /// </summary>
    Task<string?> GetTokenByWeiXinMiniAppAsync(GetTokenByWeiXinAppDto login);


    /// <summary>
    /// 利用手机号和短信验证码获取Token认证信息
    /// </summary>
    Task<string?> GetTokenBySMSAsync(GetTokenBySMSDto login);


    /// <summary>
    /// 获取授权功能列表
    /// </summary>
    Task<Dictionary<string, string>> GetFunctionListAsync(long actorUserId, string? sign);


    /// <summary>
    /// 发送短信验证手机号码所有权
    /// </summary>
    Task<bool> SendSMSVerifyCodeAsync(SendSMSVerifyCodeDto sendVerifyCode);


    /// <summary>
    /// 通过微信App Code获取Token认证信息
    /// </summary>
    Task<string?> GetTokenByWeiXinAppAsync(GetTokenByWeiXinAppDto login);


    /// <summary>
    /// 通过老密码修改密码
    /// </summary>
    Task<bool> UpdatePasswordByOldPasswordAsync(long actorUserId, UpdatePasswordByOldPasswordDto updatePassword);


    /// <summary>
    /// 通过短信验证码修改账户密码
    /// </summary>
    Task<bool> UpdatePasswordBySMSAsync(long actorUserId, UpdatePasswordBySMSDto updatePassword);


    /// <summary>
    /// 用户功能授权检测
    /// </summary>
    Task<bool> CheckFunctionAuthorizeAsync(long actorUserId, string module, string route);


    /// <summary>
    /// 签发新的Token
    /// </summary>
    Task<string?> IssueNewTokenAsync(long actorUserId, long tokenId, long notBefore, long expiresAt);

}
