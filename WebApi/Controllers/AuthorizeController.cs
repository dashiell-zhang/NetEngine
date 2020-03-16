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


                    var ret = WebApi.Libraries.Verify.JwtToken.GetToken(claim);

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


    }
}
