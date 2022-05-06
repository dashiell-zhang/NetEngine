using Common;
using Common.RedisLock.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Repository.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using WebApi.Actions.v1;
using WebApi.Filters;
using WebApi.Libraries;
using WebApi.Libraries.Verify;
using WebApi.Models.Shared;
using WebApi.Models.v1.Authorize;

namespace WebApi.Controllers.v1
{


    /// <summary>
    /// 系统访问授权模块
    /// </summary>
    [ApiVersion("1")]
    [Route("api/[controller]")]
    [ApiController]
    public class AuthorizeController : ControllerCore
    {



        /// <summary>
        /// 获取Token认证信息
        /// </summary>
        /// <param name="login">登录信息集合</param>
        /// <returns></returns>
        [HttpPost("GetToken")]
        public string? GetToken(DtoLogin login)
        {
            var userList = db.TUser.Where(t => t.IsDelete == false && (t.Name == login.Name || t.Phone == login.Name || t.Email == login.Name)).Select(t => new { t.Id, t.PassWord }).ToList();

            var user = userList.Where(t => t.PassWord == CryptoHelper.GetSHA256(t.Id.ToString() + login.PassWord)).FirstOrDefault();

            if (user != null)
            {
                return AuthorizeAction.GetTokenByUserId(user.Id);
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


            var wxinfo = weiXinHelper.GetOpenIdAndSessionKey(code);

            string openid = wxinfo.openid;
            string sessionkey = wxinfo.sessionkey;

            var user = db.TUserBindExternal.AsNoTracking().Where(t => t.IsDelete == false && t.AppName == "WeiXinMiniApp" && t.AppId == appid && t.OpenId == openid).Select(t => t.User).FirstOrDefault();

            if (user == null)
            {

                using (distLock.AcquireLock("GetTokenByWeiXinMiniAppCode" + openid))
                {
                    user = db.TUserBindExternal.AsNoTracking().Where(t => t.IsDelete == false && t.AppName == "WeiXinMiniApp" && t.AppId == appid && t.OpenId == openid).Select(t => t.User).FirstOrDefault();

                    if (user == null)
                    {

                        string userName = DateTime.UtcNow.ToString() + "微信小程序新用户";

                        //注册一个只有基本信息的账户出来
                        user = new();

                        user.Id = snowflakeHelper.GetId();
                        user.CreateTime = DateTime.UtcNow;
                        user.Name = userName;
                        user.NickName = userName;
                        user.Phone = "";
                        user.PassWord = CryptoHelper.GetSHA256(user.Id.ToString() + Guid.NewGuid().ToString());

                        db.TUser.Add(user);

                        db.SaveChanges();

                        TUserBindExternal userBind = new();
                        userBind.Id = snowflakeHelper.GetId();
                        userBind.CreateTime = DateTime.UtcNow;
                        userBind.UserId = user.Id;
                        userBind.AppName = "WeiXinMiniApp";
                        userBind.AppId = appid!;
                        userBind.OpenId = openid;


                        db.TUserBindExternal.Add(userBind);

                        db.SaveChanges();
                    }

                }

            }

            return AuthorizeAction.GetTokenByUserId(user.Id);
        }




        /// <summary>
        /// 利用手机号和短信验证码获取Token认证信息
        /// </summary>
        /// <param name="keyValue">key 为手机号，value 为验证码</param>
        /// <returns></returns>
        [HttpPost("GetTokenBySms")]
        public string? GetTokenBySms(DtoKeyValue keyValue)
        {
            if (IdentityVerification.SmsVerifyPhone(keyValue))
            {
                string phone = keyValue.Key!.ToString()!;

                var user = db.TUser.AsNoTracking().Where(t => t.IsDelete == false && (t.Name == phone || t.Phone == phone)).FirstOrDefault();

                if (user == null)
                {
                    //注册一个只有基本信息的账户出来

                    string userName = DateTime.UtcNow.ToString() + "手机短信新用户";

                    user = new();

                    user.Id = snowflakeHelper.GetId();
                    user.CreateTime = DateTime.UtcNow;
                    user.Name = userName;
                    user.NickName = userName;
                    user.Phone = phone;
                    user.PassWord = CryptoHelper.GetSHA256(user.Id.ToString() + Guid.NewGuid().ToString());

                    db.TUser.Add(user);

                    db.SaveChanges();
                }

                return AuthorizeAction.GetTokenByUserId(user.Id);
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
        /// <param name="keyValue">key 为手机号，value 可为空</param>
        /// <returns></returns>
        [HttpPost("SendSmsVerifyPhone")]
        public bool SendSmsVerifyPhone(DtoKeyValue keyValue)
        {

            string phone = keyValue.Key!.ToString()!;

            string key = "VerifyPhone_" + phone;

            if (Common.CacheHelper.IsContainKey(key) == false)
            {

                var ran = new Random();
                string code = ran.Next(100000, 999999).ToString();

                var jsonCode = new
                {
                    code
                };

                Common.AliYun.SmsHelper sms = new();
                var smsStatus = sms.SendSms(phone, "短信模板编号", "短信签名", Common.Json.JsonHelper.ObjectToJson(jsonCode));

                if (smsStatus)
                {
                    Common.CacheHelper.SetString(key, code, new TimeSpan(0, 0, 5, 0));

                    return true;
                }
                else
                {
                    return false;
                }

            }
            else
            {
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

            var accseetoken = weiXinHelper.GetAccessToken(code).accessToken;

            var openid = weiXinHelper.GetAccessToken(code).openId;

            var userInfo = weiXinHelper.GetUserInfo(accseetoken, openid);

            if (userInfo.NickName != null)
            {
                var user = db.TUserBindExternal.AsNoTracking().Where(t => t.IsDelete == false && t.AppName == "WeiXinApp" && t.AppId == appid && t.OpenId == userInfo.OpenId).Select(t => t.User).FirstOrDefault();

                if (user == null)
                {

                    user = new();
                    user.Id = snowflakeHelper.GetId();
                    user.IsDelete = false;
                    user.CreateTime = DateTime.UtcNow;
                    user.Name = userInfo.NickName;
                    user.NickName = userInfo.NickName;
                    user.Phone = "";
                    user.PassWord = CryptoHelper.GetSHA256(user.Id.ToString() + Guid.NewGuid().ToString());

                    db.TUser.Add(user);
                    db.SaveChanges();

                    TUserBindExternal bind = new();
                    bind.Id = snowflakeHelper.GetId();
                    bind.CreateTime = DateTime.UtcNow;
                    bind.AppName = "WeiXinApp";
                    bind.AppId = appid!;
                    bind.OpenId = openid;

                    bind.UserId = user.Id;

                    db.TUserBindExternal.Add(bind);

                    db.SaveChanges();
                }

                return AuthorizeAction.GetTokenByUserId(user.Id);
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
                if (user.PassWord == CryptoHelper.GetSHA256(user.Id.ToString() + updatePassWordByOldPassWord.OldPassWord))
                {
                    user.PassWord = CryptoHelper.GetSHA256(user.Id.ToString() + updatePassWordByOldPassWord.NewPassWord);
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

            var checkSms = IdentityVerification.SmsVerifyPhone(new DtoKeyValue { Key = phone, Value = updatePassWordBySms.SmsCode });

            if (checkSms)
            {
                var user = db.TUser.Where(t => t.IsDelete == false && t.Id == userId).FirstOrDefault();

                if (user != null)
                {
                    user.PassWord = CryptoHelper.GetSHA256(user.Id.ToString() + updatePassWordBySms.NewPassWord);
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
