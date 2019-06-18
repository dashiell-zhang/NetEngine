using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Models.Dtos;
using Models.JwtBearer;
using System;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;


namespace WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthorizeController : ControllerBase
    {

        private JwtSettings _jwtSettings;

        public AuthorizeController(IOptions<JwtSettings> _jwtSettingsAccesser)
        {
            _jwtSettings = _jwtSettingsAccesser.Value;
        }



        /// <summary>
        /// 获取Token认证信息
        /// </summary>
        /// <param name="login">登录信息集合</param>
        /// <returns></returns>
        [HttpPost("GetToken")]
        public string GetToken([Required][FromBody]dtoLogin login)
        {
            if (ModelState.IsValid)//判断是否合法
            {
                if (login.name == "admin" & login.password == "123456")
                {
                    var claim = new Claim[]{
                             new Claim("userid","1"),
                             new Claim("username","zhangxiaodong")
                        };

                    //对称秘钥
                    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));

                    //签名证书(秘钥，加密算法)
                    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                    //生成token
                    var token = new JwtSecurityToken(_jwtSettings.Issuer, _jwtSettings.Audience, claim, DateTime.Now, DateTime.Now.AddMinutes(30), creds);

                    var ret = new JwtSecurityTokenHandler().WriteToken(token);

                    return ret;
                }
                else
                {

                    HttpContext.Response.StatusCode = 401;

                    HttpContext.Items.Add("errMsg", "用户名或密码错误！");

                    return "";
                }


            }
            else
            {
                HttpContext.Response.StatusCode = 401;

                HttpContext.Items.Add("errMsg", "用户名或密码错误！");

                return "";
            }

        }
    }
}