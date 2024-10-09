using System.Security.Claims;

namespace WebAPIBasic.Libraries
{
    public static class ClaimsPrincipalExtension
    {


        /// <summary>
        /// 通过Key获取HttpContext.User.Claims中的信息
        /// </summary>
        /// <param name="user"></param>
        /// <param name="key">Claim关键字</param>
        /// <returns></returns>
        public static T GetClaim<T>(this ClaimsPrincipal user, string key)
        {
            var valueStr = user.FindFirst(key)?.Value;

            var value = Convert.ChangeType(valueStr, typeof(T));

            if (value != null)
            {
                return (T)value;
            }
            else
            {
                throw new Exception($"获取{key}失败");
            }

        }

    }
}
