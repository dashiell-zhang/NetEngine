using Common;
using DistributedLock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Repository.Database;
using SMS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using WebApi.Filters;
using WebApi.Libraries;
using WebApi.Models.Shared;
using WebApi.Models.v1.Authorize;
using WebApi.Services.v1;

namespace WebApi.Controllers.v1
{


    /// <summary>
    /// 系统访问授权模块
    /// </summary>
    [ApiVersion("1")]
    [Route("[controller]")]
    [ApiController]
    public class AuthorizeController : ControllerBase
    {


        private readonly DatabaseContext db;
        private readonly IDistributedLock distLock;
        private readonly SnowflakeHelper snowflakeHelper;

        private readonly IDistributedCache distributedCache;

        private readonly IHttpClientFactory httpClientFactory;

        private readonly AuthorizeService authorizeService;

        private readonly long userId;




        public AuthorizeController(DatabaseContext db, IDistributedLock distLock, SnowflakeHelper snowflakeHelper, IDistributedCache distributedCache, IHttpClientFactory httpClientFactory, AuthorizeService authorizeService, IHttpContextAccessor httpContextAccessor)
        {
            this.db = db;
            this.distLock = distLock;
            this.snowflakeHelper = snowflakeHelper;
            this.distributedCache = distributedCache;
            this.httpClientFactory = httpClientFactory;

            this.authorizeService = authorizeService;

            var userIdStr = httpContextAccessor.HttpContext?.GetClaimByAuthorization("userId");

            if (userIdStr != null)
            {
                userId = long.Parse(userIdStr);
            }
        }



        /// <summary>
        /// 获取Token认证信息
        /// </summary>
        /// <param name="login">登录信息集合</param>
        /// <returns></returns>
        [HttpPost("GetToken")]
        public string? GetToken(DtoLogin login)
        {

            var userList = db.TUser.Where(t => t.IsDelete == false && (t.Name == login.Name || t.Phone == login.Name || t.Email == login.Name)).Select(t => new { t.Id, t.PassWord }).ToList();

            var user = userList.Where(t => t.PassWord == Convert.ToBase64String(KeyDerivation.Pbkdf2(login.PassWord, Encoding.UTF8.GetBytes(t.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32))).FirstOrDefault();

            if (user != null)
            {
                return authorizeService.GetTokenByUserId(user.Id);
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
                HttpContext.Items.Add("errMsg", "用户名或密码错误");

                return default;
            }

        }



        /// <summary>
        /// 通过微信小程序Code获取Token认证信息
        /// </summary>
        /// <param name="keyValue">key 为weixinkeyid, value 为 code</param>
        /// <returns></returns>
        [HttpPost("GetTokenByWeiXinMiniAppCode")]
        public string GetTokenByWeiXinMiniAppCode([FromBody] DtoKeyValue keyValue)
        {

            var weiXinKeyId = long.Parse(keyValue.Key!.ToString()!);
            string code = keyValue.Value!.ToString()!;

            var settings = db.TAppSetting.AsNoTracking().Where(t => t.IsDelete == false && t.Module == "WeiXinMiniApp" && t.GroupId == weiXinKeyId).ToList();

            var appid = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
            var appSecret = settings.Where(t => t.Key == "AppSecret").Select(t => t.Value).FirstOrDefault();

            var weiXinHelper = new Libraries.WeiXin.MiniApp.WeiXinHelper(appid!, appSecret!);


            var wxinfo = weiXinHelper.GetOpenIdAndSessionKey(distributedCache, httpClientFactory, code);

            string openid = wxinfo.openid;
            string sessionkey = wxinfo.sessionkey;

            var user = db.TUserBindExternal.AsNoTracking().Where(t => t.IsDelete == false && t.AppName == "WeiXinMiniApp" && t.AppId == appid && t.OpenId == openid).Select(t => t.User).FirstOrDefault();

            if (user == null)
            {

                using (distLock.Lock("GetTokenByWeiXinMiniAppCode" + openid))
                {
                    user = db.TUserBindExternal.AsNoTracking().Where(t => t.IsDelete == false && t.AppName == "WeiXinMiniApp" && t.AppId == appid && t.OpenId == openid).Select(t => t.User).FirstOrDefault();

                    if (user == null)
                    {

                        string userName = DateTime.UtcNow.ToString() + "微信小程序新用户";

                        //注册一个只有基本信息的账户出来
                        user = new()
                        {
                            Id = snowflakeHelper.GetId(),
                            CreateTime = DateTime.UtcNow,
                            Name = userName,
                            NickName = userName,
                            Phone = ""
                        };
                        user.PassWord = Convert.ToBase64String(KeyDerivation.Pbkdf2(Guid.NewGuid().ToString(), Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32));

                        db.TUser.Add(user);

                        db.SaveChanges();

                        TUserBindExternal userBind = new()
                        {
                            Id = snowflakeHelper.GetId(),
                            CreateTime = DateTime.UtcNow,
                            UserId = user.Id,
                            AppName = "WeiXinMiniApp",
                            AppId = appid!,
                            OpenId = openid
                        };


                        db.TUserBindExternal.Add(userBind);

                        db.SaveChanges();
                    }

                }

            }

            return authorizeService.GetTokenByUserId(user.Id);
        }




        /// <summary>
        /// 利用手机号和短信验证码获取Token认证信息
        /// </summary>
        /// <param name="keyValue">key 为手机号，value 为验证码</param>
        /// <returns></returns>
        [HttpPost("GetTokenBySms")]
        public string? GetTokenBySms(DtoKeyValue keyValue)
        {

            string phone = keyValue.Key!.ToString()!;

            string key = "VerifyPhone_" + phone;

            var code = distributedCache.GetString(key);

            if (string.IsNullOrEmpty(code) == false && code == keyValue.Value!.ToString())
            {
                var user = db.TUser.AsNoTracking().Where(t => t.IsDelete == false && (t.Name == phone || t.Phone == phone)).FirstOrDefault();

                if (user == null)
                {
                    //注册一个只有基本信息的账户出来

                    string userName = DateTime.UtcNow.ToString() + "手机短信新用户";

                    user = new()
                    {
                        Id = snowflakeHelper.GetId(),
                        CreateTime = DateTime.UtcNow,
                        Name = userName,
                        NickName = userName,
                        Phone = phone
                    };
                    user.PassWord = Convert.ToBase64String(KeyDerivation.Pbkdf2(Guid.NewGuid().ToString(), Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32));

                    db.TUser.Add(user);

                    db.SaveChanges();
                }

                return authorizeService.GetTokenByUserId(user.Id);
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
                HttpContext.Items.Add("errMsg", "短信验证码错误");

                return default;
            }

        }




        /// <summary>
        /// 获取授权功能列表
        /// </summary>
        /// <param name="sign">模块标记</param>
        /// <returns></returns>
        [Authorize]
        [CacheDataFilter(TTL = 60, UseToken = true)]
        [HttpGet("GetFunctionList")]
        public List<DtoKeyValue> GetFunctionList(string sign)
        {

            var roleIds = db.TUserRole.AsNoTracking().Where(t => t.IsDelete == false && t.UserId == userId).Select(t => t.RoleId).ToList();

            var kvList = db.TFunctionAuthorize.Where(t => t.IsDelete == false && (roleIds.Contains(t.RoleId!.Value) || t.UserId == userId) && t.Function.Parent!.Sign == sign).Select(t => new DtoKeyValue
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
        /// <param name="keyValue">key 为手机号，value 可为空</param>
        /// <returns></returns>
        [HttpPost("SendSmsVerifyPhone")]
        public bool SendSmsVerifyPhone([FromServices] ISMS sms, DtoKeyValue keyValue)
        {

            string phone = keyValue.Key!.ToString()!;

            string key = "VerifyPhone_" + phone;

            if (distributedCache.IsContainKey(key) == false)
            {

                var ran = new Random();
                string code = ran.Next(100000, 999999).ToString();

                Dictionary<string, string> templateParams = new()
                {
                    { "code", code }
                };

                sms.SendSMS("短信签名", phone, "短信模板编号", templateParams);

                distributedCache.SetString(key, code, new TimeSpan(0, 0, 5, 0));

                return true;
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
                HttpContext.Items.Add("errMsg", "请勿频繁获取验证码！");

                return false;
            }

        }



        /// <summary>
        /// 通过微信APP Code获取Token认证信息
        /// </summary>
        /// <param name="keyValue">key 为weixinkeyid, value 为 code</param>
        /// <returns></returns>
        [HttpPost("GetTokenByWeiXinAppCode")]
        public string? GetTokenByWeiXinAppCode(DtoKeyValue keyValue)
        {

            var weiXinKeyId = long.Parse(keyValue.Key!.ToString()!);
            string code = keyValue.Value!.ToString()!;

            var settings = db.TAppSetting.AsNoTracking().Where(t => t.IsDelete == false && t.Module == "WeiXinApp" && t.GroupId == weiXinKeyId).ToList();

            var appid = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
            var appSecret = settings.Where(t => t.Key == "AppSecret").Select(t => t.Value).FirstOrDefault();

            var weiXinHelper = new Libraries.WeiXin.App.WeiXinHelper(appid!, appSecret!);

            var accseetoken = weiXinHelper.GetAccessToken(distributedCache, httpClientFactory, code).accessToken;

            var openid = weiXinHelper.GetAccessToken(distributedCache, httpClientFactory, code).openId;

            var userInfo = weiXinHelper.GetUserInfo(httpClientFactory, accseetoken, openid);

            if (userInfo.NickName != null)
            {
                var user = db.TUserBindExternal.AsNoTracking().Where(t => t.IsDelete == false && t.AppName == "WeiXinApp" && t.AppId == appid && t.OpenId == userInfo.OpenId).Select(t => t.User).FirstOrDefault();

                if (user == null)
                {

                    user = new()
                    {
                        Id = snowflakeHelper.GetId(),
                        IsDelete = false,
                        CreateTime = DateTime.UtcNow,
                        Name = userInfo.NickName,
                        NickName = userInfo.NickName,
                        Phone = ""
                    };
                    user.PassWord = Convert.ToBase64String(KeyDerivation.Pbkdf2(Guid.NewGuid().ToString(), Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32));

                    db.TUser.Add(user);
                    db.SaveChanges();

                    TUserBindExternal bind = new()
                    {
                        Id = snowflakeHelper.GetId(),
                        CreateTime = DateTime.UtcNow,
                        AppName = "WeiXinApp",
                        AppId = appid!,
                        OpenId = openid,

                        UserId = user.Id
                    };

                    db.TUserBindExternal.Add(bind);

                    db.SaveChanges();
                }

                return authorizeService.GetTokenByUserId(user.Id);
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
                HttpContext.Items.Add("errMsg", "微信授权失败");

                return default;
            }

        }




        /// <summary>
        /// 通过老密码修改密码
        /// </summary>
        /// <param name="updatePassWordByOldPassWord"></param>
        /// <returns></returns>
        [Authorize]
        [QueueLimitFilter(IsBlock = true, UseParameter = false, UseToken = true)]
        [HttpPost("UpdatePassWordByOldPassWord")]
        public bool UpdatePassWordByOldPassWord(DtoUpdatePassWordByOldPassWord updatePassWordByOldPassWord)
        {

            var user = db.TUser.Where(t => t.IsDelete == false && t.Id == userId).FirstOrDefault();

            if (user != null)
            {
                if (user.PassWord == Convert.ToBase64String(KeyDerivation.Pbkdf2(updatePassWordByOldPassWord.OldPassWord, Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32)))
                {
                    user.PassWord = Convert.ToBase64String(KeyDerivation.Pbkdf2(updatePassWordByOldPassWord.NewPassWord, Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32));
                    user.UpdateTime = DateTime.UtcNow;
                    user.UpdateUserId = user.Id;
                    db.SaveChanges();

                    return true;
                }
                else
                {
                    HttpContext.Response.StatusCode = 400;
                    HttpContext.Items.Add("errMsg", "原始密码验证失败");

                    return false;
                }
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
                HttpContext.Items.Add("errMsg", "账户异常，请联系后台工作人员");

                return false;
            }

        }



        /// <summary>
        /// 通过短信验证码修改账户密码</summary>
        /// <param name="updatePassWordBySms"></param>
        /// <returns></returns>
        [Authorize]
        [QueueLimitFilter(IsBlock = true, UseParameter = false, UseToken = true)]
        [HttpPost("UpdatePassWordBySms")]
        public bool UpdatePassWordBySms(DtoUpdatePassWordBySms updatePassWordBySms)
        {

            string phone = db.TUser.Where(t => t.Id == userId).Select(t => t.Phone).FirstOrDefault()!;

            string key = "VerifyPhone_" + phone;

            var code = distributedCache.GetString(key);


            if (string.IsNullOrEmpty(code) == false && code == updatePassWordBySms.SmsCode)
            {
                var user = db.TUser.Where(t => t.IsDelete == false && t.Id == userId).FirstOrDefault();

                if (user != null)
                {
                    user.PassWord = Convert.ToBase64String(KeyDerivation.Pbkdf2(updatePassWordBySms.NewPassWord, Encoding.UTF8.GetBytes(user.Id.ToString()), KeyDerivationPrf.HMACSHA256, 1000, 32));
                    user.UpdateTime = DateTime.UtcNow;
                    user.UpdateUserId = userId;

                    var tokenList = db.TUserToken.Where(t => t.IsDelete == false && t.UserId == userId).ToList();

                    db.TUserToken.RemoveRange(tokenList);

                    db.SaveChanges();

                    return true;
                }
                else
                {
                    HttpContext.Response.StatusCode = 400;
                    HttpContext.Items.Add("errMsg", "账户不存在");

                    return false;
                }
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
                HttpContext.Items.Add("errMsg", "短信验证码错误");

                return false;
            }

        }

    }
}
