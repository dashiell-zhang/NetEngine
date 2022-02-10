using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using WebApi.Models.AppSetting;

namespace WebApi.Libraries.Verify
{
    public class JWTToken
    {

        /// <summary>
        /// 通过Key获取Claims中的值
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string? GetClaims(string key)
        {

            try
            {
                var Authorization = Http.HttpContext.Current().Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

                var securityToken = new JwtSecurityToken(Authorization);

                var value = securityToken.Claims.ToList().Where(t => t.Type == key).FirstOrDefault()!.Value;

                return value;
            }
            catch
            {
                return null;
            }
        }



        /// <summary>
        /// 生成一个Token
        /// </summary>
        /// <returns></returns>
        public static string GetToken(Claim[] claims)
        {
            var conf = Program.ServiceProvider.GetRequiredService<IConfiguration>();

            var jwtSetting = conf.GetSection("JWTSetting").Get<JWTSetting>();

            //对称秘钥
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSetting.SecretKey));

            //签名证书(秘钥，加密算法)
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            //生成token
            var token = new JwtSecurityToken(jwtSetting.Issuer, jwtSetting.Audience, claims, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(30), creds);

            var ret = new JwtSecurityTokenHandler().WriteToken(token);

            return ret;
        }

    }
}
