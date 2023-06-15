using Common;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using Repository.Database;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using WebAPI.Models.AppSetting;
using WebAPI.Models.Authorize;

namespace WebAPI.Services
{

    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class AuthorizeService
    {

        private readonly DatabaseContext db;
        private readonly IDHelper idHelper;
        private readonly IConfiguration configuration;
        private readonly HttpClient httpClient;
        private readonly IDistributedCache distributedCache;


        public AuthorizeService(DatabaseContext db, IDHelper idHelper, IConfiguration configuration, IHttpClientFactory httpClientFactory, IDistributedCache distributedCache)
        {
            this.db = db;
            this.idHelper = idHelper;
            this.configuration = configuration;
            httpClient = httpClientFactory.CreateClient();
            this.distributedCache = distributedCache;
        }


        /// <summary>
        /// 通过用户id获取 token
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public string GetTokenByUserId(long userId)
        {
            TUserToken userToken = new()
            {
                Id = idHelper.GetId(),
                UserId = userId,
                CreateTime = DateTime.UtcNow
            };

            db.TUserToken.Add(userToken);
            db.SaveChanges();

            var claims = new Claim[]
            {
                new Claim("tokenId",userToken.Id.ToString()),
                new Claim("userId",userId.ToString())
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
        public (string openId, string sessionKey) GetWeiXinMiniAPPOpenIdAndSessionKey(string appId, string code)
        {
            var settingGroupId = db.TAppSetting.AsNoTracking().Where(t => t.Module == "WeiXinMiniAPP" && t.Key == "APPId" && t.Value == appId).Select(t => t.GroupId).FirstOrDefault();

            var appSecret = db.TAppSetting.AsNoTracking().Where(t => t.Module == "WeiXinMiniAPP" && t.GroupId == settingGroupId && t.Key == "APPSecret").Select(t => t.Value).FirstOrDefault();

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

            throw new Exception("GetWeiXinMiniAPPOpenIdAndSessionKey 获取失败");
        }



        /// <summary>
        /// 获取微信APP AccessToken和OpenId
        /// </summary>
        /// <returns></returns>
        public (string accessToken, string openId) GetWeiXinAPPAccessTokenAndOpenId(string appId, string code)
        {
            var settingGroupId = db.TAppSetting.AsNoTracking().Where(t => t.Module == "WeiXinAPP" && t.Key == "APPId" && t.Value == appId).Select(t => t.GroupId).FirstOrDefault();

            var appSecret = db.TAppSetting.AsNoTracking().Where(t => t.Module == "WeiXinAPP" && t.GroupId == settingGroupId && t.Key == "APPSecret").Select(t => t.Value).FirstOrDefault();

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
        /// 获取微信APP 用户信息
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="openId"></param>
        /// <returns></returns>
        public DtoGetWeiXinAPPUserInfo GetWeiXinAPPUserInfo(string accessToken, string openId)
        {
            string url = "https://api.weixin.qq.com/sns/userinfo?access_token=" + accessToken + "&openid=" + openId;

            var returnJson = httpClient.GetAsync(url).Result.Content.ReadAsStringAsync().Result;

            var userInfo = JsonHelper.JsonToObject<DtoGetWeiXinAPPUserInfo>(returnJson);

            return userInfo;
        }

    }
}
