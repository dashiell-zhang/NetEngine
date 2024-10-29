using Shared.Model;
using Shared.Model.Authorize;

namespace Shared.Interface
{
    public interface IAuthorizeService
    {

        /// <summary>
        /// 获取公钥
        /// </summary>
        /// <returns></returns>
        public string? GetPublicKey();



        /// <summary>
        /// 获取Token认证信息
        /// </summary>
        /// <param name="login"></param>
        /// <returns></returns>
        public string? GetToken(DtoGetToken login);



        /// <summary>
        /// 通过微信小程序Code获取Token认证信息
        /// </summary>
        /// <param name="login"></param>
        /// <returns></returns>
        public string? GetTokenByWeiXinMiniApp(DtoGetTokenByWeiXinApp login);



        /// <summary>
        /// 发送短信验证手机号码所有权
        /// </summary>
        /// <param name="sms"></param>
        /// <param name="sendVerifyCode"></param>
        /// <returns></returns>
        public bool SendSMSVerifyCode(DtoSendSMSVerifyCode sendVerifyCode);




        /// <summary>
        /// 通过微信App Code获取Token认证信息
        /// </summary>
        /// <param name="login"></param>
        /// <returns></returns>
        public string? GetTokenByWeiXinApp(DtoGetTokenByWeiXinApp login);



        /// <summary>
        /// 通过老密码修改密码
        /// </summary>
        /// <param name="updatePassword"></param>
        /// <returns></returns>
        public bool UpdatePasswordByOldPassword(DtoUpdatePasswordByOldPassword updatePassword);



        /// <summary>
        /// 通过短信验证码修改账户密码</summary>
        /// <param name="updatePassword"></param>
        /// <returns></returns>
        public bool UpdatePasswordBySMS(DtoUpdatePasswordBySMS updatePassword);



        /// <summary>
        /// 生成密码
        /// </summary>
        /// <param name="passWord"></param>
        /// <returns></returns>
        public DtoKeyValue GeneratePassword(string passWord);



        /// <summary>
        /// 获取授权功能列表
        /// </summary>
        /// <param name="sign">模块标记</param>
        /// <returns></returns>
        public List<DtoKeyValue> GetFunctionList(string? sign = null);


        /// <summary>
        /// 利用手机号和短信验证码获取Token认证信息
        /// </summary>
        /// <param name="login"></param>
        /// <returns></returns>
        public string? GetTokenBySMS(DtoGetTokenBySMS login);


        /// <summary>
        /// 通过用户id获取 token
        /// </summary>
        /// <param name="userId">用户Id</param>
        /// <param name="lastTokenId">上一次的TokenId</param>
        /// <returns></returns>
        public string GetTokenByUserId(long userId, long? lastTokenId = null);


        /// <summary>
        /// 获取微信小程序用户OpenId 和 SessionKey
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="code">登录时获取的 code，可通过wx.login获取</param>
        /// <returns></returns>
        public (string openId, string sessionKey) GetWeiXinMiniAppOpenIdAndSessionKey(string appId, string code);


        /// <summary>
        /// 获取微信App AccessToken和OpenId
        /// </summary>
        /// <returns></returns>
        public (string accessToken, string openId) GetWeiXinAppAccessTokenAndOpenId(string appId, string code);


        /// <summary>
        /// 获取微信App 用户信息
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="openId"></param>
        /// <returns></returns>
        public DtoGetWeiXinAppUserInfo GetWeiXinAppUserInfo(string accessToken, string openId);


    }
}
