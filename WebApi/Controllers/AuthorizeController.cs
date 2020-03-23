using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Repository.WebCore;
using Models.Dtos;
using System;
using System.Linq;
using System.Security.Claims;


namespace WebApi.Controllers
{


    /// <summary>
    /// 系统访问授权模块
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class AuthorizeController : ControllerBase
    {

        /// <summary>
        /// 获取Token认证信息
        /// </summary>
        /// <param name="login">登录信息集合</param>
        /// <returns></returns>
        [HttpPost("GetToken")]
        public string GetToken([FromBody] dtoLogin login)
        {


            using (var db = new webcoreContext())
            {
                var user = db.TUser.Where(t => t.Name == login.name & t.PassWord == login.password).FirstOrDefault();

                if (user != null)
                {
                    TUserToken userToken = new TUserToken();
                    userToken.Id = Guid.NewGuid().ToString();
                    userToken.UserId = user.Id;
                    userToken.CreateTime = DateTime.Now;

                    db.TUserToken.Add(userToken);
                    db.SaveChanges();

                    var claim = new Claim[]{
                        new Claim("tokenid",userToken.Id),
                             new Claim("userid",user.Id)
                        };


                    var ret = Libraries.Verify.JwtToken.GetToken(claim);

                    return ret;
                }
                else
                {

                    HttpContext.Response.StatusCode = 400;

                    HttpContext.Items.Add("errMsg", "用户名或密码错误！");

                    return "";
                }
            }

        }



        /// <summary>
        /// 通过微信OpenId获取Token认证信息
        /// </summary>
        /// <param name="keyValue">key 为weixinkeyid, value 为 code</param>
        /// <returns></returns>
        [HttpPost("GetTokenByWeiXinOpenId")]
        public string GetTokenByWeiXinOpenId([FromBody] dtoKeyValue keyValue)
        {


            using (var db = new webcoreContext())
            {
                string weixinkeyid = keyValue.Key.ToString();
                string code = keyValue.Value.ToString();

                var weixinkey = db.TWeiXinKey.Where(t => t.Id == weixinkeyid).FirstOrDefault();

                var weiXinHelper = new Libraries.WeiXin.MiniApp.WeiXinHelper(weixinkey.WxAppId, weixinkey.WxAppSecret);


                var wxinfo = weiXinHelper.GetOpenIdAndSessionKey(code);

                string openid = wxinfo.openid;
                string sessionkey = wxinfo.sessionkey;

                var user = db.TUserBindWeixin.Where(t => t.WeiXinOpenId == openid).Select(t => t.User).FirstOrDefault();


                if (user != null)
                {
                    TUserToken userToken = new TUserToken();
                    userToken.Id = Guid.NewGuid().ToString();
                    userToken.UserId = user.Id;
                    userToken.CreateTime = DateTime.Now;

                    db.TUserToken.Add(userToken);
                    db.SaveChanges();

                    var claim = new Claim[]{
                        new Claim("tokenid",userToken.Id),
                             new Claim("userid",user.Id)
                        };


                    var ret = Libraries.Verify.JwtToken.GetToken(claim);

                    return ret;
                }
                else
                {

                    HttpContext.Response.StatusCode = 400;

                    HttpContext.Items.Add("errMsg", "获取授权失败！");

                    return "";
                }

            }




        }
    }
}
