using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Repository.WebCore;
using System.Linq;
using WebApi.Filters;
using WebApi.Libraries.WeiXin.MiniApp;
using WebApi.Models.User;

namespace WebApi.Controllers
{
    /// <summary>
    /// 用户数据操作控制器
    /// </summary>
    [Authorize]
    [JwtTokenVerify]
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {


        /// <summary>
        /// 获取 微信Openid
        /// </summary>
        /// <param name="weixinkeyid">微信配置密钥ID</param>
        /// <param name="code">微信临时code</param>
        /// <returns>openid,userid</returns>
        /// <remarks>传入租户ID和微信临时 code 获取 openid，如果 openid 在系统有中对应用户，则一并返回用户的ID值，否则用户ID值为空</remarks>
        [HttpGet("GetWeiXinOpenId")]
        public (string openid, string userid) GetWeiXinOpenId(string weixinkeyid, string code)
        {
            using (var db = new webcoreContext())
            {

                var weixinkey = db.TWeiXinKey.Where(t => t.Id == weixinkeyid).FirstOrDefault();

                WeiXinHelper weiXinHelper = new WeiXinHelper(weixinkey.WxAppId, weixinkey.WxAppSecret);


                var wxinfo = weiXinHelper.GetOpenIdAndSessionKey(code);

                string openid = wxinfo.openid;
                string sessionkey = wxinfo.sessionkey;

                string userid = "";

                var userinfo = db.TUserBindWeixin.Where(t => t.WeiXinOpenId == openid).Select(t => t.User).FirstOrDefault();
                if (userinfo != null)
                {
                    userid = userinfo.Id;
                }

                return (openid, userid);

            }

        }


        /// <summary>
        /// 获取微信账户绑定的手机号
        /// </summary>
        /// <param name="iv">加密算法的初始向量</param>
        /// <param name="encryptedData">包括敏感数据在内的完整用户信息的加密数据</param>
        /// <param name="code">微信临时code</param>
        /// <param name="weixinkeyid">微信配置密钥ID</param>
        [HttpGet("GetWeiXinPhone")]
        public string GetWeiXinPhone(string iv, string encryptedData, string code, string weixinkeyid)
        {

            using (var db = new webcoreContext())
            {
                var weixinkey = db.TWeiXinKey.Where(t => t.Id == weixinkeyid).FirstOrDefault();

                WeiXinHelper weiXinHelper = new WeiXinHelper(weixinkey.WxAppId, weixinkey.WxAppSecret);


                var wxinfo = weiXinHelper.GetOpenIdAndSessionKey(code);

                string openid = wxinfo.openid;
                string sessionkey = wxinfo.sessionkey;


                var strJson = WeiXinHelper.DecryptionData(encryptedData, sessionkey, iv);

                var user = db.TUserBindWeixin.Where(t => t.WeiXinOpenId == openid & t.WeiXinKeyId == weixinkeyid).Select(t => t.User).FirstOrDefault();


                var xxx = db.TUser.Where(t => t.Id == "xxx").Include(t => t.TUserBindWeixin).FirstOrDefault();

                user.Phone = Common.Json.JsonHelper.GetValueByKey(strJson, "phoneNumber");

                db.SaveChanges();

                return user.Phone;

            }
        }



        /// <summary>
        /// 通过 UserId 获取用户信息 
        /// </summary>
        /// <param name="userid">用户ID</param>
        /// <returns></returns>
        [HttpGet("GetUser")]
        [CacheData(TTL = 60, UseToken = true)]
        public dtoUser GetUser(string userid)
        {
            using (var db = new webcoreContext())
            {
                if (string.IsNullOrEmpty(userid))
                {
                    userid = WebApi.Libraries.Verify.JwtToken.GetClaims("userid");
                }

                var user = db.TUser.Where(t => t.Id == userid && t.IsDelete == false).Select(t => new dtoUser
                {
                    Name = t.Name,
                    NickName = t.NickName,
                    Phone = t.Phone,
                    Email = t.Email,
                    Role = t.Role,
                    CreateTime = t.CreateTime
                }).FirstOrDefault();

                return user;
            }
        }


    }
}