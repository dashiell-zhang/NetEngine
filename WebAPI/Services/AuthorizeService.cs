using Common;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Repository.Database;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using WebAPI.Models.Authorize;
using WebAPIBasic.Models.AppSetting;

namespace WebAPI.Services
{

    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class AuthorizeService(DatabaseContext db, IdService idService, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        private readonly HttpClient httpClient = httpClientFactory.CreateClient();


        /// <summary>
        /// 通过用户id获取 token
        /// </summary>
        /// <param name="userId">用户Id</param>
        /// <param name="lastTokenId">上一次的TokenId</param>
        /// <returns></returns>
        public string GetTokenByUserId(long userId, long? lastTokenId = null)
        {

            TUserToken userToken = new()
            {
                Id = idService.GetId(),
                UserId = userId,
                LastId = lastTokenId
            };

            db.TUserToken.Add(userToken);
            db.SaveChanges();

            var claims = new Claim[]
            {
                new("tokenId",userToken.Id.ToString()),
                new("userId",userId.ToString())
            };

            var jwtSetting = configuration.GetRequiredSection("JWT").Get<JWTSetting>()!;

            var jwtPrivateKey = ECDsa.Create();
            jwtPrivateKey.ImportECPrivateKey(Convert.FromBase64String(jwtSetting.PrivateKey), out _);
            SigningCredentials creds = new(new ECDsaSecurityKey(jwtPrivateKey), SecurityAlgorithms.EcdsaSha256);
            JwtSecurityToken jwtSecurityToken = new(jwtSetting.Issuer, jwtSetting.Audience, claims, DateTime.UtcNow, DateTime.UtcNow + jwtSetting.Expiry, creds);

            return new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken);
        }





        /// <summary>
        /// 获取微信小程序用户OpenId 和 SessionKey
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="code">登录时获取的 code，可通过wx.login获取</param>
        /// <returns></returns>
        public (string openId, string sessionKey) GetWeiXinMiniAppOpenIdAndSessionKey(string appId, string code)
        {
            var settingGroupId = db.TAppSetting.AsNoTracking().Where(t => t.Module == "WeiXinMiniApp" && t.Key == "AppId" && t.Value == appId).Select(t => t.GroupId).FirstOrDefault();

            var appSecret = db.TAppSetting.AsNoTracking().Where(t => t.Module == "WeiXinMiniApp" && t.GroupId == settingGroupId && t.Key == "AppSecret").Select(t => t.Value).FirstOrDefault();

            if (appSecret != null)
            {

                string url = "https://api.weixin.qq.com/sns/jscode2session?appid=" + appId + "&secret=" + appSecret + "&js_code=" + code + "&grant_type=authorization_code";

                string httpRet = httpClient.GetAsync(url).Result.Content.ReadAsStringAsync().Result;

                var openId = JsonHelper.GetValueByKey(httpRet, "openid");
                var sessionKey = JsonHelper.GetValueByKey(httpRet, "session_key");

                if (openId != null && sessionKey != null)
                {
                    return (openId, sessionKey);
                }
            }

            throw new Exception("GetWeiXinMiniAppOpenIdAndSessionKey 获取失败");
        }



        /// <summary>
        /// 获取微信App AccessToken和OpenId
        /// </summary>
        /// <returns></returns>
        public (string accessToken, string openId) GetWeiXinAppAccessTokenAndOpenId(string appId, string code)
        {
            var settingGroupId = db.TAppSetting.AsNoTracking().Where(t => t.Module == "WeiXinApp" && t.Key == "AppId" && t.Value == appId).Select(t => t.GroupId).FirstOrDefault();

            var appSecret = db.TAppSetting.AsNoTracking().Where(t => t.Module == "WeiXinApp" && t.GroupId == settingGroupId && t.Key == "AppSecret").Select(t => t.Value).FirstOrDefault();

            if (appSecret != null)
            {
                string url = "https://api.weixin.qq.com/sns/oauth2/access_token?appid=" + appId + "&secret=" + appSecret + "&code=" + code + "&grant_type=authorization_code";

                var returnJson = httpClient.GetAsync(url).Result.Content.ReadAsStringAsync().Result;

                var accessToken = JsonHelper.GetValueByKey(returnJson, "access_token");
                var openId = JsonHelper.GetValueByKey(returnJson, "openid");

                if (accessToken != null && openId != null)
                {
                    return (accessToken, openId);
                }
            }
            throw new Exception("获取 AccessToken 失败");
        }



        /// <summary>
        /// 获取微信App 用户信息
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="openId"></param>
        /// <returns></returns>
        public DtoGetWeiXinAppUserInfo GetWeiXinAppUserInfo(string accessToken, string openId)
        {
            string url = "https://api.weixin.qq.com/sns/userinfo?access_token=" + accessToken + "&openid=" + openId;

            var returnJson = httpClient.GetAsync(url).Result.Content.ReadAsStringAsync().Result;

            var userInfo = JsonHelper.JsonToObject<DtoGetWeiXinAppUserInfo>(returnJson);

            return userInfo;
        }

    }
}
