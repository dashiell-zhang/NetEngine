using Common;
using DistributedLock;
using IdentifierGenerator;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Repository.Database;
using Shared.Interface;
using Shared.Model;
using Shared.Model.AppSetting;
using Shared.Model.Authorize;
using SMS;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Shared.Service
{

    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class AuthorizeService(DatabaseContext db, IUserContext userContext, IDistributedCache distributedCache, IdService idService, IConfiguration configuration, IHttpClientFactory httpClientFactory, IDistributedLock distLock, ISMS sms) : IAuthorizeService
    {

        private long userId => userContext.UserId;


        private readonly HttpClient httpClient = httpClientFactory.CreateClient();


        public string? GetPublicKey()
        {
            try
            {
                var rsaSetting = configuration.GetRequiredSection("RSA").Get<RSASetting>();

                return rsaSetting?.PublicKey;
            }
            catch
            {
                throw new CustomException("RSA公钥加载异常");
            }
        }




        /// <summary>
        /// 获取Token认证信息
        /// </summary>
        /// <param name="login"></param>
        /// <returns></returns>
        public string? GetToken(DtoGetToken login)
        {
            var userList = db.TUser.Where(t => t.UserName == login.UserName).Select(t => new { t.Id, t.Password }).ToList();

            var user = userList.Where(t => t.Password == Convert.ToBase64String(KeyDerivation.Pbkdf2(login.Password, Encoding.UTF8.GetBytes(t.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32))).FirstOrDefault();

            if (user != null)
            {
                return GetTokenByUserId(user.Id);
            }
            else
            {
                throw new CustomException("用户名或密码错误");
            }
        }



        /// <summary>
        /// 通过微信小程序Code获取Token认证信息
        /// </summary>
        /// <param name="login"></param>
        /// <returns></returns>
        public string? GetTokenByWeiXinMiniApp(DtoGetTokenByWeiXinApp login)
        {
            var (openId, sessionKey) = GetWeiXinMiniAppOpenIdAndSessionKey(login.AppId, login.Code);

            var userIdQuery = db.TUserBindExternal.Where(t => t.AppName == "WeiXinMiniApp" && t.AppId == login.AppId && t.OpenId == login.AppId).Select(t => t.User.Id);

            var userId = userIdQuery.FirstOrDefault();

            if (userId == default)
            {

                using (distLock.Lock("GetTokenByWeiXinMiniAppCode" + openId))
                {
                    userId = userIdQuery.FirstOrDefault();

                    if (userId == default)
                    {
                        string userName = DateTime.UtcNow.ToString() + "微信小程序新用户";

                        //注册一个只有基本信息的账户出来
                        TUser user = new()
                        {
                            Id = idService.GetId(),
                            Name = userName,
                            UserName = Guid.NewGuid().ToString(),
                            Phone = ""
                        };
                        user.Password = Convert.ToBase64String(KeyDerivation.Pbkdf2(Guid.NewGuid().ToString(), Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32));

                        db.TUser.Add(user);

                        db.SaveChanges();

                        TUserBindExternal userBind = new()
                        {
                            Id = idService.GetId(),
                            UserId = user.Id,
                            AppName = "WeiXinMiniApp",
                            AppId = login.AppId,
                            OpenId = openId
                        };

                        db.TUserBindExternal.Add(userBind);

                        db.SaveChanges();

                        userId = user.Id;
                    }

                }

            }

            if (userId != default)
            {
                return GetTokenByUserId(userId);

            }
            else
            {
                throw new CustomException("获取Token失败");
            }
        }




        /// <summary>
        /// 利用手机号和短信验证码获取Token认证信息
        /// </summary>
        /// <param name="login"></param>
        /// <returns></returns>
        public string? GetTokenBySMS(DtoGetTokenBySMS login)
        {
            string key = "VerifyPhone_" + login.Phone;

            var code = distributedCache.GetString(key);

            if (string.IsNullOrEmpty(code) == false && code == login.VerifyCode)
            {
                var userId = db.TUser.Where(t => t.Phone == login.Phone).Select(t => t.Id).FirstOrDefault();

                if (userId == default)
                {
                    //注册一个只有基本信息的账户出来
                    string userName = DateTime.UtcNow.ToString() + "手机短信新用户";

                    TUser user = new()
                    {
                        Id = idService.GetId(),
                        Name = userName,
                        UserName = Guid.NewGuid().ToString(),
                        Phone = login.Phone
                    };
                    user.Password = Convert.ToBase64String(KeyDerivation.Pbkdf2(Guid.NewGuid().ToString(), Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32));

                    db.TUser.Add(user);

                    db.SaveChanges();

                    userId = user.Id;
                }

                if (userId != default)
                {
                    return GetTokenByUserId(userId);
                }
                else
                {
                    throw new CustomException("系统异常暂时无法登录");
                }
            }
            else
            {
                throw new CustomException("短信验证码错误");
            }
        }




        /// <summary>
        /// 获取授权功能列表
        /// </summary>
        /// <param name="sign">模块标记</param>
        /// <returns></returns>
        public List<DtoKeyValue> GetFunctionList(string? sign)
        {

            var roleIds = db.TUserRole.AsNoTracking().Where(t => t.UserId == userId).Select(t => t.RoleId).ToList();

            var query = db.TFunctionAuthorize.Where(t => (roleIds.Contains(t.RoleId!.Value) || t.UserId == userId));

            if (sign != null)
            {
                query = query.Where(t => t.Function.Sign == sign);
            }

            var kvList = query.Select(t => new DtoKeyValue
            {
                Key = t.Function.Sign,
                Value = t.Function.Name
            }).ToList();

            return kvList;
        }




        /// <summary>
        /// 发送短信验证手机号码所有权
        /// </summary>
        /// <param name="sms"></param>
        /// <param name="sendVerifyCode"></param>
        /// <returns></returns>
        public bool SendSMSVerifyCode(DtoSendSMSVerifyCode sendVerifyCode)
        {
            string key = "VerifyPhone_" + sendVerifyCode.Phone;

            if (distributedCache.IsContainKey(key) == false)
            {
                Random ran = new();
                string code = ran.Next(100000, 999999).ToString();

                Dictionary<string, string> templateParams = new()
                {
                    { "code", code }
                };

                sms.SendSMS("短信签名", sendVerifyCode.Phone, "短信模板编号", templateParams);

                distributedCache.Set(key, code, new TimeSpan(0, 0, 5, 0));

                return true;
            }
            else
            {
                throw new CustomException("请勿频繁获取验证码！");
            }

        }



        /// <summary>
        /// 通过微信App Code获取Token认证信息
        /// </summary>
        /// <param name="login"></param>
        /// <returns></returns>
        public string? GetTokenByWeiXinApp(DtoGetTokenByWeiXinApp login)
        {
            var (accessToken, openId) = GetWeiXinAppAccessTokenAndOpenId(login.AppId, login.Code);

            var userInfo = GetWeiXinAppUserInfo(accessToken, openId);

            if (userInfo.NickName != null)
            {
                var user = db.TUserBindExternal.AsNoTracking().Where(t => t.AppName == "WeiXinApp" && t.AppId == login.AppId && t.OpenId == userInfo.OpenId).Select(t => t.User).FirstOrDefault();

                if (user == null)
                {

                    user = new()
                    {
                        Id = idService.GetId(),
                        IsDelete = false,
                        Name = userInfo.NickName,
                        UserName = Guid.NewGuid().ToString(),
                        Phone = ""
                    };
                    user.Password = Convert.ToBase64String(KeyDerivation.Pbkdf2(Guid.NewGuid().ToString(), Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32));

                    db.TUser.Add(user);
                    db.SaveChanges();

                    TUserBindExternal bind = new()
                    {
                        Id = idService.GetId(),
                        AppName = "WeiXinApp",
                        AppId = login.AppId,
                        OpenId = openId,

                        UserId = user.Id
                    };

                    db.TUserBindExternal.Add(bind);

                    db.SaveChanges();
                }

                return GetTokenByUserId(user.Id);
            }

            throw new CustomException("微信授权失败");

        }




        /// <summary>
        /// 通过老密码修改密码
        /// </summary>
        /// <param name="updatePassword"></param>
        /// <returns></returns>
        public bool UpdatePasswordByOldPassword(DtoUpdatePasswordByOldPassword updatePassword)
        {

            var user = db.TUser.Where(t => t.Id == userId).FirstOrDefault();

            if (user != null)
            {
                if (user.Password == Convert.ToBase64String(KeyDerivation.Pbkdf2(updatePassword.OldPassword, Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32)))
                {
                    user.Password = Convert.ToBase64String(KeyDerivation.Pbkdf2(updatePassword.NewPassword, Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32));
                    user.UpdateUserId = user.Id;
                    db.SaveChanges();

                    return true;
                }
                else
                {
                    throw new CustomException("原始密码验证失败");
                }
            }
            else
            {
                throw new CustomException("账户异常，请联系后台工作人员");
            }

        }



        /// <summary>
        /// 通过短信验证码修改账户密码</summary>
        /// <param name="updatePassword"></param>
        /// <returns></returns>
        public bool UpdatePasswordBySMS(DtoUpdatePasswordBySMS updatePassword)
        {

            string phone = db.TUser.Where(t => t.Id == userId).Select(t => t.Phone).FirstOrDefault()!;

            string key = "VerifyPhone_" + phone;

            var code = distributedCache.GetString(key);


            if (string.IsNullOrEmpty(code) == false && code == updatePassword.SmsCode)
            {
                var user = db.TUser.Where(t => t.Id == userId).FirstOrDefault();

                if (user != null)
                {
                    user.Password = Convert.ToBase64String(KeyDerivation.Pbkdf2(updatePassword.NewPassword, Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32));
                    user.UpdateUserId = userId;

                    var tokenList = db.TUserToken.Where(t => t.UserId == userId).ToList();

                    db.TUserToken.RemoveRange(tokenList);

                    db.SaveChanges();

                    return true;
                }
                else
                {
                    throw new CustomException("账户不存在");
                }
            }
            else
            {
                throw new CustomException("短信验证码错误");
            }

        }




        /// <summary>
        /// 生成密码
        /// </summary>
        /// <param name="passWord"></param>
        /// <returns></returns>
        public DtoKeyValue GeneratePassword(string passWord)
        {
            DtoKeyValue keyValue = new()
            {
                Key = idService.GetId()
            };

            keyValue.Value = Convert.ToBase64String(KeyDerivation.Pbkdf2(passWord, Encoding.UTF8.GetBytes(keyValue.Key.ToString()!), KeyDerivationPrf.HMACSHA256, 1000, 32));

            return keyValue;
        }







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

            SigningCredentials signingCredentials = new(new ECDsaSecurityKey(jwtPrivateKey), SecurityAlgorithms.EcdsaSha256);

            var nowTime = DateTime.UtcNow;

            SecurityTokenDescriptor tokenDescriptor = new()
            {
                IssuedAt = nowTime,
                Issuer = jwtSetting.Issuer,
                Audience = jwtSetting.Audience,
                NotBefore = nowTime,
                Subject = new ClaimsIdentity(claims),
                Expires = nowTime + jwtSetting.Expiry,
                SigningCredentials = signingCredentials
            };

            JsonWebTokenHandler jwtTokenHandler = new();

            var jwtToken = jwtTokenHandler.CreateToken(tokenDescriptor);

            return jwtToken;
        }



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



        public DtoGetWeiXinAppUserInfo GetWeiXinAppUserInfo(string accessToken, string openId)
        {
            string url = "https://api.weixin.qq.com/sns/userinfo?access_token=" + accessToken + "&openid=" + openId;

            var returnJson = httpClient.GetAsync(url).Result.Content.ReadAsStringAsync().Result;

            var userInfo = JsonHelper.JsonToObject<DtoGetWeiXinAppUserInfo>(returnJson);

            return userInfo;
        }
    }
}
