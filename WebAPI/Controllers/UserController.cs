using Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Repository.Database;
using WebAPI.Filters;
using WebAPI.Libraries;
using WebAPI.Models.Shared;
using WebAPI.Models.User;

namespace WebAPI.Controllers
{


    /// <summary>
    /// 用户数据操作控制器
    /// </summary>
    [Route("[controller]")]
    [Authorize]
    [ApiController]
    public class UserController : ControllerBase
    {


        private readonly DatabaseContext db;

        private readonly IDistributedCache distributedCache;

        private readonly IHttpClientFactory httpClientFactory;


        private readonly long userId;



        public UserController(DatabaseContext db, IDistributedCache distributedCache, IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
        {
            this.db = db;
            this.distributedCache = distributedCache;
            this.httpClientFactory = httpClientFactory;

            var userIdStr = httpContextAccessor.HttpContext?.GetClaimByAuthorization("userId");
            if (userIdStr != null)
            {
                userId = long.Parse(userIdStr);
            }
        }



        /// <summary>
        /// 获取微信小程序OpenId
        /// </summary>
        /// <param name="weiXinKeyId">微信配置密钥ID</param>
        /// <param name="code">微信临时code</param>
        /// <returns>openid,userid</returns>
        /// <remarks>传入租户ID和微信临时 code 获取 openid，如果 openid 在系统有中对应用户，则一并返回用户的ID值，否则用户ID值为空</remarks>
        [HttpGet("GetWeiXinMiniAppOpenId")]
        public string? GetWeiXinMiniAppOpenId(long weiXinKeyId, string code)
        {

            var settings = db.TAppSetting.AsNoTracking().Where(t => t.IsDelete == false && t.Module == "WeiXinMiniApp" && t.GroupId == weiXinKeyId).ToList();

            var appid = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
            var appSecret = settings.Where(t => t.Key == "AppSecret").Select(t => t.Value).FirstOrDefault();


            if (appid != null && appSecret != null)
            {
                Libraries.WeiXin.MiniApp.WeiXinHelper weiXinHelper = new(appid, appSecret);

                var wxinfo = weiXinHelper.GetOpenIdAndSessionKey(distributedCache, httpClientFactory, code);

                string openid = wxinfo.openid;

                return openid;
            }
            else
            {
                return null;
            }
        }



        /// <summary>
        /// 获取微信小程序手机号
        /// </summary>
        /// <param name="iv">加密算法的初始向量</param>
        /// <param name="encryptedData">包括敏感数据在内的完整用户信息的加密数据</param>
        /// <param name="code">微信临时code</param>
        /// <param name="weiXinKeyId">微信配置密钥ID</param>
        [HttpGet("GetWeiXinMiniAppPhone")]
        public string? GetWeiXinMiniAppPhone(string iv, string encryptedData, string code, long weiXinKeyId)
        {

            var settings = db.TAppSetting.AsNoTracking().Where(t => t.IsDelete == false && t.Module == "WeiXinMiniApp" && t.GroupId == weiXinKeyId).ToList();

            var appId = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
            var appSecret = settings.Where(t => t.Key == "AppSecret").Select(t => t.Value).FirstOrDefault();


            if (appId != null && appSecret != null)
            {
                Libraries.WeiXin.MiniApp.WeiXinHelper weiXinHelper = new(appId, appSecret);


                var wxinfo = weiXinHelper.GetOpenIdAndSessionKey(distributedCache, httpClientFactory, code);

                string openid = wxinfo.openid;
                string sessionkey = wxinfo.sessionkey;

                var strJson = Libraries.WeiXin.MiniApp.WeiXinHelper.DecryptionData(encryptedData, sessionkey, iv);

                var user = db.TUserBindExternal.Where(t => t.IsDelete == false && t.OpenId == openid && t.AppName == "WeiXinMiniApp" && t.AppId == appId).Select(t => t.User).FirstOrDefault();

                if (user != null)
                {
                    var retPhone = JsonHelper.GetValueByKey(strJson, "phoneNumber");

                    if (retPhone != null)
                    {
                        user.Phone = retPhone;
                        db.SaveChanges();
                        return user.Phone;
                    }

                }
            }

            return null;
        }



        /// <summary>
        /// 通过 UserId 获取用户信息 
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns></returns>
        [HttpGet("GetUser")]
        [CacheDataFilter(TTL = 60, IsUseToken = true)]
        public DtoUser? GetUser(long? userId)
        {

            userId ??= this.userId;

            var user = db.TUser.Where(t => t.Id == userId && t.IsDelete == false).Select(t => new DtoUser
            {
                Name = t.Name,
                UserName = t.UserName,
                Phone = t.Phone,
                Email = t.Email,
                Roles = string.Join(",", db.TUserRole.Where(r => r.IsDelete == false && r.UserId == t.Id).Select(r => r.Role.Name).ToList()),
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
        public bool EditUserPhoneBySms([FromBody] DtoKeyValue keyValue)
        {

            string phone = keyValue.Key!.ToString()!;

            string key = "VerifyPhone_" + phone;

            var code = distributedCache.GetString(key);


            if (string.IsNullOrEmpty(code) == false && code == keyValue.Value!.ToString())
            {

                var checkPhone = db.TUser.Where(t => t.Id != userId && t.Phone == phone).Count();

                var user = db.TUser.Where(t => t.Id == userId).FirstOrDefault();

                if (user != null)
                {
                    if (checkPhone == 0)
                    {
                        user.Phone = phone;

                        db.SaveChanges();

                        return true;
                    }
                    else
                    {
                        HttpContext.SetErrMsg("手机号已被其他账户绑定");

                        return false;
                    }
                }
                else
                {
                    HttpContext.SetErrMsg("账户不存在");

                    return false;
                }
            }
            else
            {
                HttpContext.SetErrMsg("短信验证码错误");

                return false;
            }
        }


    }
}