using Application.Model.Authorize.Authorize;

namespace Application.Core.Interfaces.Authorize
{
    public interface IAuthorizeService
    {

        /// <summary>
        /// 获取公钥
        /// </summary>
        /// <returns></returns>
        string? GetPublicKey();



        /// <summary>
        /// 获取Token认证信息
        /// </summary>
        /// <param name="login"></param>
        /// <returns></returns>
        Task<string?> GetTokenAsync(DtoGetToken login);



        /// <summary>
        /// 通过微信小程序Code获取Token认证信息
        /// </summary>
        /// <param name="login"></param>
        /// <returns></returns>
        Task<string?> GetTokenByWeiXinMiniAppAsync(DtoGetTokenByWeiXinApp login);



        /// <summary>
        /// 发送短信验证手机号码所有权
        /// </summary>
        /// <param name="sms"></param>
        /// <param name="sendVerifyCode"></param>
        /// <returns></returns>
        Task<bool> SendSMSVerifyCodeAsync(DtoSendSMSVerifyCode sendVerifyCode);




        /// <summary>
        /// 通过微信App Code获取Token认证信息
        /// </summary>
        /// <param name="login"></param>
        /// <returns></returns>
        Task<string?> GetTokenByWeiXinAppAsync(DtoGetTokenByWeiXinApp login);



        /// <summary>
        /// 通过老密码修改密码
        /// </summary>
        /// <param name="updatePassword"></param>
        /// <returns></returns>
        Task<bool> UpdatePasswordByOldPasswordAsync(DtoUpdatePasswordByOldPassword updatePassword);



        /// <summary>
        /// 通过短信验证码修改账户密码</summary>
        /// <param name="updatePassword"></param>
        /// <returns></returns>
        Task<bool> UpdatePasswordBySMSAsync(DtoUpdatePasswordBySMS updatePassword);



        /// <summary>
        /// 获取授权功能列表
        /// </summary>
        /// <param name="sign">模块标记</param>
        /// <returns></returns>
        Task<Dictionary<string, string>> GetFunctionListAsync(string? sign = null);


        /// <summary>
        /// 利用手机号和短信验证码获取Token认证信息
        /// </summary>
        /// <param name="login"></param>
        /// <returns></returns>
        Task<string?> GetTokenBySMSAsync(DtoGetTokenBySMS login);


        /// <summary>
        /// 通过用户id获取 token
        /// </summary>
        /// <param name="userId">用户Id</param>
        /// <param name="lastTokenId">上一次的TokenId</param>
        /// <returns></returns>
        Task<string> GetTokenByUserIdAsync(long userId, long? lastTokenId = null);


        /// <summary>
        /// 获取微信小程序用户OpenId 和 SessionKey
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="code">登录时获取的 code，可通过wx.login获取</param>
        /// <returns></returns>
        Task<(string openId, string sessionKey)> GetWeiXinMiniAppOpenIdAndSessionKeyAsync(string appId, string code);


        /// <summary>
        /// 获取微信App AccessToken和OpenId
        /// </summary>
        /// <returns></returns>
        Task<(string accessToken, string openId)> GetWeiXinAppAccessTokenAndOpenIdAsync(string appId, string code);


        /// <summary>
        /// 获取微信App 用户信息
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="openId"></param>
        /// <returns></returns>
        Task<DtoGetWeiXinAppUserInfo> GetWeiXinAppUserInfoAsync(string accessToken, string openId);


        /// <summary>
        /// 用户功能授权检测
        /// </summary>
        /// <returns></returns>
        Task<bool> CheckFunctionAuthorizeAsync(string module, string route);


        /// <summary>
        /// 签发新的Token
        /// </summary>
        /// <returns></returns>
        Task<string?> IssueNewTokenAsync();

    }
}
