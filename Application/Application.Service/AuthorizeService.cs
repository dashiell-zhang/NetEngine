using Application.Interface;
using Application.Model.AppSetting;
using Application.Model.Authorize;
using Application.Model.Message;
using Application.Service.TaskCenter;
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
using Repository;
using Repository.Database;
using SourceGenerator.Runtime.Attributes;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Application.Service;

[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class AuthorizeService(DatabaseContext db, IUserContext userContext, IDistributedCache distributedCache, IdService idService, IConfiguration configuration, IHttpClientFactory httpClientFactory, IDistributedLock distLock, QueueTaskService queueTaskService)
{


    private readonly HttpClient httpClient = httpClientFactory.CreateClient();


    /// <summary>
    /// 获取公钥
    /// </summary>
    /// <returns></returns>
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
    public async Task<string?> GetTokenAsync(GetTokenDto login)
    {
        var userList = await db.User.Where(t => t.UserName == login.UserName).Select(t => new { t.Id, t.Password }).ToListAsync();

        var user = userList.Where(t => t.Password == Convert.ToBase64String(KeyDerivation.Pbkdf2(login.Password, Encoding.UTF8.GetBytes(t.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32))).FirstOrDefault();

        if (user != null)
        {
            return await GetTokenByUserIdAsync(user.Id);
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
    public async Task<string?> GetTokenByWeiXinMiniAppAsync(GetTokenByWeiXinAppDto login)
    {
        var (openId, sessionKey) = await GetWeiXinMiniAppOpenIdAndSessionKeyAsync(login.AppId, login.Code);

        var userIdQuery = db.UserBindExternal.Where(t => t.AppName == "WeiXinMiniApp" && t.AppId == login.AppId && t.OpenId == login.AppId).Select(t => t.User.Id);

        var userId = await userIdQuery.FirstOrDefaultAsync();

        if (userId == default)
        {

            using (await distLock.LockAsync("GetTokenByWeiXinMiniAppCode" + openId))
            {

                userId = await userIdQuery.FirstOrDefaultAsync();

                if (userId == default)
                {
                    string userName = DateTime.UtcNow.ToString() + "微信小程序新用户";

                    //注册一个只有基本信息的账户出来
                    Repository.Database.User user = new()
                    {
                        Id = idService.GetId(),
                        Name = userName,
                        UserName = Guid.NewGuid().ToString(),
                        Phone = ""
                    };
                    user.Password = Convert.ToBase64String(KeyDerivation.Pbkdf2(Guid.NewGuid().ToString(), Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32));

                    db.User.Add(user);

                    UserBindExternal userBind = new()
                    {
                        Id = idService.GetId(),
                        UserId = user.Id,
                        AppName = "WeiXinMiniApp",
                        AppId = login.AppId,
                        OpenId = openId
                    };

                    db.UserBindExternal.Add(userBind);

                    await db.SaveChangesAsync();

                    userId = user.Id;
                }

            }

        }

        if (userId != default)
        {
            return await GetTokenByUserIdAsync(userId);

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
    public async Task<string?> GetTokenBySMSAsync(GetTokenBySMSDto login)
    {
        string key = "VerifyPhone_" + login.Phone;

        var code = await distributedCache.GetStringAsync(key);

        if (string.IsNullOrEmpty(code) == false && code == login.VerifyCode)
        {
            var userId = await db.User.Where(t => t.Phone == login.Phone).Select(t => t.Id).FirstOrDefaultAsync();

            if (userId == default)
            {
                //注册一个只有基本信息的账户出来
                string userName = DateTime.UtcNow.ToString() + "手机短信新用户";

                Repository.Database.User user = new()
                {
                    Id = idService.GetId(),
                    Name = userName,
                    UserName = Guid.NewGuid().ToString(),
                    Phone = login.Phone
                };
                user.Password = Convert.ToBase64String(KeyDerivation.Pbkdf2(Guid.NewGuid().ToString(), Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32));

                db.User.Add(user);

                await db.SaveChangesAsync();

                userId = user.Id;
            }

            if (userId != default)
            {
                return await GetTokenByUserIdAsync(userId);
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
    public async Task<Dictionary<string, string>> GetFunctionListAsync(string? sign)
    {

        var roleIds = await db.UserRole.AsNoTracking().Where(t => t.UserId == userContext.UserId).Select(t => t.RoleId).ToListAsync();

        var query = db.FunctionAuthorize.Where(t => roleIds.Contains(t.RoleId!.Value) || t.UserId == userContext.UserId);

        if (sign != null)
        {
            query = query.Where(t => t.Function.Sign == sign);
        }

        var kvList = (await query.Select(t => new
        {
            Key = t.Function.Sign,
            Value = t.Function.Name
        }).ToListAsync()).DistinctBy(t => t.Key).ToList();

        var keyValues = kvList.ToDictionary(item => item.Key, item => item.Value);

        return keyValues;
    }


    /// <summary>
    /// 发送短信验证手机号码所有权
    /// </summary>
    /// <param name="sms"></param>
    /// <param name="sendVerifyCode"></param>
    /// <returns></returns>
    public async Task<bool> SendSMSVerifyCodeAsync(SendSMSVerifyCodeDto sendVerifyCode)
    {
        string key = "VerifyPhone_" + sendVerifyCode.Phone;

        if (await distributedCache.IsContainKeyAsync(key) == false)
        {
            Random ran = new();
            string code = ran.Next(100000, 999999).ToString();

            Dictionary<string, string> templateParams = new()
            {
                { "code", code }
            };

            SendSMSDto sendSMS = new()
            {
                SignName = "",
                Phone = sendVerifyCode.Phone,
                TemplateCode = "",
                TemplateParams = templateParams
            };

            var sendSMSTask = queueTaskService.CreateSingleAsync("MessageTask.SendSMS", sendSMS);

            var setCacheTask = distributedCache.SetAsync(key, code, new TimeSpan(0, 0, 5, 0));

            await Task.WhenAll(sendSMSTask, setCacheTask);

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
    public async Task<string?> GetTokenByWeiXinAppAsync(GetTokenByWeiXinAppDto login)
    {
        var (accessToken, openId) = await GetWeiXinAppAccessTokenAndOpenIdAsync(login.AppId, login.Code);

        var userInfo = await GetWeiXinAppUserInfoAsync(accessToken, openId);

        if (userInfo.NickName != null)
        {
            var user = await db.UserBindExternal.AsNoTracking().Where(t => t.AppName == "WeiXinApp" && t.AppId == login.AppId && t.OpenId == userInfo.OpenId).Select(t => t.User).FirstOrDefaultAsync();

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

                db.User.Add(user);

                UserBindExternal bind = new()
                {
                    Id = idService.GetId(),
                    AppName = "WeiXinApp",
                    AppId = login.AppId,
                    OpenId = openId,

                    UserId = user.Id
                };

                db.UserBindExternal.Add(bind);

                await db.SaveChangesAsync();
            }

            return await GetTokenByUserIdAsync(user.Id);
        }

        throw new CustomException("微信授权失败");

    }


    /// <summary>
    /// 通过老密码修改密码
    /// </summary>
    /// <param name="updatePassword"></param>
    /// <returns></returns>
    public async Task<bool> UpdatePasswordByOldPasswordAsync(UpdatePasswordByOldPasswordDto updatePassword)
    {

        var user = await db.User.Where(t => t.Id == userContext.UserId).FirstOrDefaultAsync();

        if (user != null)
        {
            if (user.Password == Convert.ToBase64String(KeyDerivation.Pbkdf2(updatePassword.OldPassword, Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32)))
            {
                user.Password = Convert.ToBase64String(KeyDerivation.Pbkdf2(updatePassword.NewPassword, Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32));
                user.UpdateUserId = user.Id;

                await db.SaveChangesAsync();

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
    public async Task<bool> UpdatePasswordBySMSAsync(UpdatePasswordBySMSDto updatePassword)
    {

        string phone = await db.User.Where(t => t.Id == userContext.UserId).Select(t => t.Phone).FirstAsync();

        string key = "VerifyPhone_" + phone;

        var code = await distributedCache.GetStringAsync(key);


        if (string.IsNullOrEmpty(code) == false && code == updatePassword.SmsCode)
        {
            var user = await db.User.Where(t => t.Id == userContext.UserId).FirstOrDefaultAsync();

            if (user != null)
            {
                user.Password = Convert.ToBase64String(KeyDerivation.Pbkdf2(updatePassword.NewPassword, Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32));
                user.UpdateUserId = userContext.UserId;

                var tokenList = await db.UserToken.Where(t => t.UserId == userContext.UserId).ToListAsync();

                db.UserToken.RemoveRange(tokenList);

                await db.SaveChangesAsync();

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
    /// 通过用户id获取 token
    /// </summary>
    /// <param name="userId">用户Id</param>
    /// <param name="lastTokenId">上一次的TokenId</param>
    /// <returns></returns>
    public async Task<string> GetTokenByUserIdAsync(long userId, long? lastTokenId = null)
    {

        UserToken userToken = new()
        {
            Id = idService.GetId(),
            UserId = userId,
            LastId = lastTokenId
        };

        db.UserToken.Add(userToken);

        await db.SaveChangesAsync();

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


    /// <summary>
    /// 获取微信小程序用户OpenId 和 SessionKey
    /// </summary>
    /// <param name="appId"></param>
    /// <param name="code">登录时获取的 code，可通过wx.login获取</param>
    /// <returns></returns>
    public async Task<(string openId, string sessionKey)> GetWeiXinMiniAppOpenIdAndSessionKeyAsync(string appId, string code)
    {
        var settingGroupId = await db.AppSetting.AsNoTracking().Where(t => t.Module == "WeiXinMiniApp" && t.Key == "AppId" && t.Value == appId).Select(t => t.GroupId).FirstOrDefaultAsync();

        var appSecret = await db.AppSetting.AsNoTracking().Where(t => t.Module == "WeiXinMiniApp" && t.GroupId == settingGroupId && t.Key == "AppSecret").Select(t => t.Value).FirstOrDefaultAsync();

        if (appSecret != null)
        {

            string url = "https://api.weixin.qq.com/sns/jscode2session?appid=" + appId + "&secret=" + appSecret + "&js_code=" + code + "&grant_type=authorization_code";

            var httpResponseMessage = await httpClient.GetAsync(url);

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                string httpRet = await httpResponseMessage.Content.ReadAsStringAsync();

                var openId = JsonHelper.GetValueByKey(httpRet, "openid");
                var sessionKey = JsonHelper.GetValueByKey(httpRet, "session_key");

                if (openId != null && sessionKey != null)
                {
                    return (openId, sessionKey);
                }
            }
        }

        throw new Exception("GetWeiXinMiniAppOpenIdAndSessionKey 获取失败");
    }


    /// <summary>
    /// 获取微信App AccessToken和OpenId
    /// </summary>
    /// <returns></returns>
    public async Task<(string accessToken, string openId)> GetWeiXinAppAccessTokenAndOpenIdAsync(string appId, string code)
    {
        var settingGroupId = await db.AppSetting.AsNoTracking().Where(t => t.Module == "WeiXinApp" && t.Key == "AppId" && t.Value == appId).Select(t => t.GroupId).FirstOrDefaultAsync();

        var appSecret = await db.AppSetting.AsNoTracking().Where(t => t.Module == "WeiXinApp" && t.GroupId == settingGroupId && t.Key == "AppSecret").Select(t => t.Value).FirstOrDefaultAsync();

        if (appSecret != null)
        {
            string url = "https://api.weixin.qq.com/sns/oauth2/access_token?appid=" + appId + "&secret=" + appSecret + "&code=" + code + "&grant_type=authorization_code";

            var httpResponseMessage = await httpClient.GetAsync(url);

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                var returnJson = await httpResponseMessage.Content.ReadAsStringAsync();

                var accessToken = JsonHelper.GetValueByKey(returnJson, "access_token");
                var openId = JsonHelper.GetValueByKey(returnJson, "openid");

                if (accessToken != null && openId != null)
                {
                    return (accessToken, openId);
                }
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
    public async Task<GetWeiXinAppUserInfoDto> GetWeiXinAppUserInfoAsync(string accessToken, string openId)
    {
        string url = "https://api.weixin.qq.com/sns/userinfo?access_token=" + accessToken + "&openid=" + openId;

        var httpResponseMessage = await httpClient.GetAsync(url);

        if (httpResponseMessage.IsSuccessStatusCode)
        {
            var returnJson = await httpResponseMessage.Content.ReadAsStringAsync();

            var userInfo = JsonHelper.JsonToObject<GetWeiXinAppUserInfoDto>(returnJson);

            return userInfo;
        }
        else
        {
            throw new Exception("获取微信用户信息失败");
        }
    }


    /// <summary>
    /// 用户功能授权检测
    /// </summary>
    /// <returns></returns>
    public async Task<bool> CheckFunctionAuthorizeAsync(string module, string route)
    {

        var userId = userContext.UserId;

        var functionId = await db.FunctionRoute.Where(t => t.Module == module && t.Route == route).Select(t => t.FunctionId).FirstOrDefaultAsync();

        if (functionId != default)
        {
            var roleIds = await db.UserRole.Where(t => t.UserId == userId).Select(t => t.RoleId).ToListAsync();

            var functionAuthorizeId = await db.FunctionAuthorize.Where(t => t.FunctionId == functionId && (roleIds.Contains(t.RoleId!.Value) || t.UserId == userId)).Select(t => t.Id).FirstOrDefaultAsync();

            if (functionAuthorizeId != default)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            return true;
        }
    }


    /// <summary>
    /// 签发新的Token
    /// </summary>
    /// <returns></returns>
    public async Task<string?> IssueNewTokenAsync()
    {
        var nbf = long.Parse(userContext.Claims.First(t => t.Type == "nbf").Value);
        var exp = long.Parse(userContext.Claims.First(t => t.Type == "exp").Value);

        var nbfTime = DateTimeOffset.FromUnixTimeSeconds(nbf);
        var expTime = DateTimeOffset.FromUnixTimeSeconds(exp);

        var lifeSpan = nbfTime + (expTime - nbfTime) * 0.5;

        //当前Token有效期不足一半时签发新的Token
        if (lifeSpan < DateTimeOffset.UtcNow)
        {

            var tokenId = long.Parse(userContext.Claims.First(t => t.Type == "tokenId").Value);
            var userId = userContext.UserId;

            string key = "IssueNewToken" + tokenId;

            using var lockHandle = await distLock.TryLockAsync(key);
            if (lockHandle != null)
            {
                var newToken = await distributedCache.GetStringAsync(tokenId + "newToken");

                if (newToken == null)
                {
                    newToken = await GetTokenByUserIdAsync(userId, tokenId);

                    if (await distLock.TryLockAsync("ClearExpireToken") != null)
                    {
                        var clearTime = DateTime.UtcNow.AddDays(-7);
                        var clearList = await db.UserToken.Where(t => t.CreateTime < clearTime).ToListAsync();
                        db.UserToken.RemoveRange(clearList);

                        await db.SaveChangesAsync();
                    }

                    await distributedCache.SetAsync(tokenId + "newToken", newToken, TimeSpan.FromMinutes(10));
                }

                return newToken;
            }
        }

        return null;
    }
}
