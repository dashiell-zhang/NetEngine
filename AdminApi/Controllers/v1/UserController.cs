using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Dtos;
using System;
using System.Linq;
using AdminApi.Filters;
using AdminApi.Libraries;
using AdminApi.Libraries.Verify;
using AdminApi.Models.v1.User;

namespace AdminApi.Controllers.v1
{


    /// <summary>
    /// 用户数据操作控制器
    /// </summary>
    [ApiVersion("1")]
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class UserController : ControllerCore
    {



        /// <summary>
        /// 通过 UserId 获取用户信息 
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns></returns>
        [HttpGet("GetUser")]
        [CacheDataFilter(TTL = 60, UseToken = true)]
        public dtoUser GetUser(Guid? userId)
        {

            if (userId == null)
            {
                userId = base.userId;
            }

            var user = db.TUser.Where(t => t.Id == userId && t.IsDelete == false).Select(t => new dtoUser
            {
                Name = t.Name,
                NickName = t.NickName,
                Phone = t.Phone,
                Email = t.Email,
                Roles = string.Join(",", db.TUserRole.Where(r => r.IsDelete == false & r.UserId == t.Id).Select(r => r.Role.Name).ToList()),
                CreateTime = t.CreateTime
            }).FirstOrDefault();

            return user;
        }



        /// <summary>
        /// 通过短信验证码修改账户手机号
        /// </summary>
        /// <param name="keyValue">key 为新手机号，value 为短信验证码</param>
        /// <returns></returns>
        [HttpPost("EditUserPhoneBySms")]
        public bool EditUserPhoneBySms([FromBody] dtoKeyValue keyValue)
        {

            if (IdentityVerification.SmsVerifyPhone(keyValue))
            {

                string phone = keyValue.Key.ToString();


                var checkPhone = db.TUser.Where(t => t.Id != userId && t.Phone == phone).Count();

                var user = db.TUser.Where(t => t.Id == userId).FirstOrDefault();


                var isMergeUser = false;

                if (isMergeUser)
                {
                    //获取目标手机号绑定的账户ID
                    var phoneUserId = db.TUser.Where(t => t.Phone == phone).Select(t => t.Id).FirstOrDefault();

                    user.Phone = phone;

                    db.SaveChanges();

                    //如果目标手机号绑定用户，则进行数据合并动作
                    if (phoneUserId != default)
                    {
                        //将手机号对应的用户移除，合并数据到新的账号
                        Common.DBHelper.MergeUser(phoneUserId, user.Id);
                    }

                    return true;
                }
                else
                {
                    if (checkPhone == 0)
                    {
                        user.Phone = phone;

                        db.SaveChanges();

                        return true;
                    }
                    else
                    {
                        HttpContext.Response.StatusCode = 400;
                        HttpContext.Items.Add("errMsg", "User.EditUserPhoneBySms.'The target mobile number has been bound by another account'");

                        return false;
                    }
                }


            }
            else
            {
                HttpContext.Response.StatusCode = 400;
                HttpContext.Items.Add("errMsg", "User.EditUserPhoneBySms.'Error in SMS verification code'");

                return false;
            }
        }




        /// <summary>
        /// 通过短信验证码修改账户密码</summary>
        /// <param name="keyValue">key为新密码，value为短信验证码</param>
        /// <returns></returns>
        [HttpPost("EditUserPassWordBySms")]
        public bool EditUserPassWordBySms([FromBody] dtoKeyValue keyValue)
        {


            string phone = db.TUser.Where(t => t.Id == userId).Select(t => t.Phone).FirstOrDefault();

            string smsCode = keyValue.Value.ToString();

            var checkSms = IdentityVerification.SmsVerifyPhone(new dtoKeyValue { Key = phone, Value = smsCode });

            if (checkSms)
            {
                string password = keyValue.Key.ToString();

                if (!string.IsNullOrEmpty(password))
                {
                    var user = db.TUser.Where(t => t.IsDelete == false & t.Id == userId).FirstOrDefault();

                    user.PassWord = password;

                    db.SaveChanges();


                    var tokenList = db.TUserToken.Where(t => t.IsDelete == false & t.UserId == userId).ToList();

                    db.TUserToken.RemoveRange(tokenList);

                    db.SaveChanges();

                    return true;
                }
                else
                {
                    HttpContext.Response.StatusCode = 400;
                    HttpContext.Items.Add("errMsg", "User.EditUserPassWordBySms.'New password is not allowed to be empty'");

                    return false;
                }
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
                HttpContext.Items.Add("errMsg", "User.EditUserPassWordBySms.'Error in SMS verification code'");

                return false;
            }

        }

    }
}