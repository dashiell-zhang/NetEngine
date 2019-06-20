using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;

namespace Methods.Verify
{
    public class JwtToken
    {

        /// <summary>
        /// 通过Key获取Claims中的值
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string GetClaims(string key)
        {

            try
            {
                var Authorization = Http.HttpContext.Current().Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

                var securityToken = new JwtSecurityToken(Authorization);

                var value = securityToken.Claims.ToList().Where(t => t.Type == key).FirstOrDefault().Value;

                return value;
            }
            catch
            {
                return null;
            }
        }

    }
}
