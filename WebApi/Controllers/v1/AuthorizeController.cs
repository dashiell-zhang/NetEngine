using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models.Dtos;
using Repository.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using WebApi.Filters;
using WebApi.Libraries.Verify;

namespace WebApi.Controllers.v1
{


    /// <summary>
    /// 系统访问授权模块
    /// </summary>
    [ApiVersion("1")]
    [Route("api/[controller]")]
    [ApiController]
    public class AuthorizeController : ControllerBase
    {


        private readonly dbContext db;

        public AuthorizeController(dbContext context)
        {
            db = context;
        }


        /// <summary>
        /// 获取Token认证信息
        /// </summary>
        /// <param name="login">登录信息集合</param>
        /// <returns></returns>
        [HttpPost("GetToken")]
        public string GetToken([FromBody] dtoLogin login)
        {

            var user = db.TUser.Where(t => t.IsDelete == false & (t.Name == login.Name || t.Phone == login.Name || t.Email == login.Name) && t.PassWord == login.PassWord).FirstOrDefault();

            if (user != null)
            {
                TUserToken userToken = new TUserToken();
                userToken.Id = Guid.NewGuid();
                userToken.UserId = user.Id;
                userToken.CreateTime = DateTime.Now;

                db.TUserToken.Add(userToken);
                db.SaveChanges();

                var claim = new Claim[]{
                        new Claim("tokenId",userToken.Id.ToString()),
                             new Claim("userId",user.Id.ToString())
                        };


                var ret = Libraries.Verify.JWTToken.GetToken(claim);

                return ret;
            }
            else
            {

                HttpContext.Response.StatusCode = 400;

                HttpContext.Items.Add("errMsg", "Authorize.GetToken.'Wrong user name or password'");

                return "";
            }

        }



        /// <summary>
        /// 通过微信小程序Code获取Token认证信息
        /// </summary>
        /// <param name="keyValue">key 为weixinkeyid, value 为 code</param>
        /// <returns></returns>
        [HttpPost("GetTokenByWeiXinMiniAppCode")]
        public string GetTokenByWeiXinMiniAppCode([FromBody] dtoKeyValue keyValue)
        {

            var weiXinKeyId = Guid.Parse(keyValue.Key.ToString());
            string code = keyValue.Value.ToString();

            var settings = db.TAppSetting.Where(t => t.IsDelete == false & t.Module == "WeiXinMiniApp" & t.GroupId == weiXinKeyId).ToList();

            var appid = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
            var appSecret = settings.Where(t => t.Key == "AppSecret").Select(t => t.Value).FirstOrDefault();

            var weiXinHelper = new Libraries.WeiXin.MiniApp.WeiXinHelper(appid, appSecret);


            var wxinfo = weiXinHelper.GetOpenIdAndSessionKey(code);

            string openid = wxinfo.openid;
            string sessionkey = wxinfo.sessionkey;

            var user = db.TUserBindExternal.Where(t => t.IsDelete == false & t.AppName == "WeiXinMiniApp" & t.AppId == appid & t.OpenId == openid).Select(t => t.User).FirstOrDefault();

            if (user == null)
            {

                bool isAction = false;

                while (isAction == false)
                {
                    if (Common.RedisHelper.Lock("GetTokenByWeiXinMiniAppCode" + openid, "123456", TimeSpan.FromSeconds(5)))
                    {
                        isAction = true;

                        user = db.TUserBindExternal.Where(t => t.IsDelete == false & t.AppName == "WeiXinMiniApp" & t.AppId == appid & t.OpenId == openid).Select(t => t.User).FirstOrDefault();

                        if (user == null)
                        {
                            //注册一个只有基本信息的账户出来
                            user = new TUser();

                            user.Id = Guid.NewGuid();
                            user.IsDelete = false;
                            user.CreateTime = DateTime.Now;
                            user.Name = DateTime.Now.ToString() + "微信小程序新用户";
                            user.NickName = user.Name;
                            user.PassWord = Guid.NewGuid().ToString();


                            db.TUser.Add(user);

                            db.SaveChanges();

                            var userBind = new TUserBindExternal();
                            userBind.Id = Guid.NewGuid();
                            userBind.CreateTime = DateTime.Now;
                            userBind.UserId = user.Id;
                            userBind.AppName = "WeiXinMiniApp";
                            userBind.AppId = appid;
                            userBind.OpenId = openid;

                            db.TUserBindExternal.Add(userBind);

                            db.SaveChanges();
                        }

                        Common.RedisHelper.UnLock("GetTokenByWeiXinMiniAppCode" + openid, "123456");
                    }
                    else
                    {
                        Thread.Sleep(500);
                    }
                }

            }

            return GetToken(new dtoLogin { Name = user.Name, PassWord = user.PassWord });
        }




        /// <summary>
        /// 利用手机号和短信验证码获取Token认证信息
        /// </summary>
        /// <param name="keyValue">key 为手机号，value 为验证码</param>
        /// <returns></returns>
        [HttpPost("GetTokenBySms")]
        public string GetTokenBySms(dtoKeyValue keyValue)
        {
            if (IdentityVerification.SmsVerifyPhone(keyValue))
            {
                string phone = keyValue.Key.ToString();

                var user = db.TUser.Where(t => t.IsDelete == false && (t.Name == phone || t.Phone == phone)).FirstOrDefault();

                if (user == null)
                {
                    //注册一个只有基本信息的账户出来

                    user = new TUser();

                    user.Id = Guid.NewGuid();
                    user.IsDelete = false;
                    user.CreateTime = DateTime.Now;
                    user.Name = DateTime.Now.ToString() + "手机短信新用户";
                    user.NickName = user.Name;
                    user.PassWord = Guid.NewGuid().ToString();
                    user.Phone = phone;

                    db.TUser.Add(user);

                    db.SaveChanges();
                }

                return GetToken(new dtoLogin { Name = user.Name, PassWord = user.PassWord });
            }
            else
            {
                HttpContext.Response.StatusCode = 400;

                HttpContext.Items.Add("errMsg", "Authorize.GetTokenBySms.'New password is not allowed to be empty'");

                return "";
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
        public List<dtoKeyValue> GetFunctionList(string sign)
        {
            var userId = Guid.Parse(Libraries.Verify.JWTToken.GetClaims("userId"));

            var roleIds = db.TUserRole.Where(t => t.IsDelete == false & t.UserId == userId).Select(t => t.RoleId).ToList();

            var kvList = db.TFunctionAuthorize.Where(t => t.IsDelete == false & (roleIds.Contains(t.RoleId.Value) | t.UserId == userId) & t.Function.Parent.Sign == sign).Select(t => new dtoKeyValue
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
        public bool SendSmsVerifyPhone(dtoKeyValue keyValue)
        {

            string phone = keyValue.Key.ToString();

            string key = "VerifyPhone_" + phone;

            if (Common.RedisHelper.IsContainString(key) == false)
            {

                Random ran = new Random();
                string code = ran.Next(100000, 999999).ToString();

                var jsonCode = new
                {
                    code = code
                };

                Common.AliYun.SmsHelper sms = new Common.AliYun.SmsHelper();
                var smsStatus = sms.SendSms(phone, "短信模板编号", "短信签名", Common.Json.JsonHelper.ObjectToJSON(jsonCode));

                if (smsStatus)
                {
                    Common.RedisHelper.StringSet(key, code, new TimeSpan(0, 0, 5, 0));

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
        public string GetTokenByWeiXinAppCode(dtoKeyValue keyValue)
        {

            var weiXinKeyId = Guid.Parse(keyValue.Key.ToString());
            string code = keyValue.Value.ToString();

            var settings = db.TAppSetting.Where(t => t.IsDelete == false & t.Module == "WeiXinApp" & t.GroupId == weiXinKeyId).ToList();

            var appid = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
            var appSecret = settings.Where(t => t.Key == "AppSecret").Select(t => t.Value).FirstOrDefault();

            var weiXinHelper = new Libraries.WeiXin.App.WeiXinHelper(appid, appSecret);

            var accseetoken = weiXinHelper.GetAccessToken(code).accessToken;

            var openid = weiXinHelper.GetAccessToken(code).openId;

            var userInfo = weiXinHelper.GetUserInfo(accseetoken, openid);

            var user = db.TUserBindExternal.Where(t => t.IsDelete == false && t.AppName == "WeiXinApp" & t.AppId == appid & t.OpenId == userInfo.openid).Select(t => t.User).FirstOrDefault();

            if (user == null)
            {
                user = new TUser();
                user.Id = Guid.NewGuid();
                user.IsDelete = false;
                user.CreateTime = DateTime.Now;

                user.Name = userInfo.nickname;
                user.NickName = user.Name;
                user.PassWord = Guid.NewGuid().ToString();

                db.TUser.Add(user);
                db.SaveChanges();

                var bind = new TUserBindExternal();
                bind.Id = Guid.NewGuid();
                bind.CreateTime = DateTime.Now;

                bind.AppName = "WeiXinApp";
                bind.AppId = appid;
                bind.UserId = user.Id;
                bind.OpenId = openid;

                db.TUserBindExternal.Add(bind);

                db.SaveChanges();
            }

            return GetToken(new dtoLogin { Name = user.Name, PassWord = user.PassWord });

        }



    }
}
