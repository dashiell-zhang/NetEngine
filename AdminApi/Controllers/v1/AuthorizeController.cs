using AdminApi.Filters;
using AdminApi.Libraries;
using AdminApi.Libraries.Verify;
using AdminShared.Models;
using AdminShared.Models.v1.Authorize;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Repository.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace AdminApi.Controllers.v1
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
        public string GetToken([FromBody] DtoLogin login)
        {

            var user = db.TUser.AsNoTracking().Where(t => t.IsDelete == false & (t.Name == login.Name || t.Phone == login.Name || t.Email == login.Name) && t.PassWord == login.PassWord).FirstOrDefault();

            if (user != null)
            {
                TUserToken userToken = new();
                userToken.Id = snowflakeHelper.GetId();
                userToken.UserId = user.Id;
                userToken.CreateTime = DateTime.UtcNow;

                db.TUserToken.Add(userToken);
                db.SaveChanges();

                var claim = new Claim[]
                {
                    new Claim("tokenId",userToken.Id.ToString()),
                    new Claim("userId",user.Id.ToString())
                };


                var ret = JWTToken.GetToken(claim);

                return ret;
            }
            else
            {

                HttpContext.Response.StatusCode = 400;
                HttpContext.Items.Add("errMsg", "用户名或密码错误");

                return "";
            }

        }




        /// <summary>
        /// 利用手机号和短信验证码获取Token认证信息
        /// </summary>
        /// <param name="keyValue">key 为手机号，value 为验证码</param>
        /// <returns></returns>
        [HttpPost("GetTokenBySms")]
        public string GetTokenBySms(DtoKeyValue keyValue)
        {
            if (IdentityVerification.SmsVerifyPhone(keyValue))
            {
                string phone = keyValue.Key.ToString();

                var user = db.TUser.AsNoTracking().Where(t => t.IsDelete == false && (t.Name == phone || t.Phone == phone)).FirstOrDefault();

                if (user == null)
                {
                    //注册一个只有基本信息的账户出来

                    user = new();

                    user.Id = snowflakeHelper.GetId();
                    user.CreateTime = DateTime.UtcNow;
                    user.Name = DateTime.UtcNow.ToString() + "手机短信新用户";
                    user.NickName = user.Name;
                    user.PassWord = Guid.NewGuid().ToString();
                    user.Phone = phone;

                    db.TUser.Add(user);

                    db.SaveChanges();
                }

                return GetToken(new DtoLogin { Name = user.Name, PassWord = user.PassWord });
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
                HttpContext.Items.Add("errMsg", "短信验证码错误");

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
        public List<DtoKeyValue> GetFunctionList(string sign)
        {

            var roleIds = db.TUserRole.AsNoTracking().Where(t => t.IsDelete == false & t.UserId == userId).Select(t => t.RoleId).ToList();

            var kvList = db.TFunctionAuthorize.Where(t => t.IsDelete == false & (roleIds.Contains(t.RoleId.Value) | t.UserId == userId) & t.Function.Parent.Sign == sign).Select(t => new DtoKeyValue
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

            string phone = keyValue.Key.ToString();

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





    }
}
