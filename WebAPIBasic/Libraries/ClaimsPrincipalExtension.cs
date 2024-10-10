using System.Security.Claims;

namespace WebAPIBasic.Libraries
{
    public static class ClaimsPrincipalExtension
    {


        /// <summary>
        /// 通过Key获取Claims中的信息
        /// </summary>
        /// <param name="claims"></param>
        /// <param name="key">Claim关键字</param>
        /// <returns></returns>
        public static T GetClaim<T>(this ClaimsPrincipal claims, string key) where T : IConvertible
        {
            var valueStr = claims.FindFirst(key)?.Value;

            if (valueStr != null)
            {
                try
                {
                    object value;

                    switch (Type.GetTypeCode(typeof(T)))
                    {
                        case TypeCode.String:
                            value = valueStr;
                            break;

                        case TypeCode.Boolean:
                            value = bool.Parse(valueStr);
                            break;

                        case TypeCode.Int32:
                            value = int.Parse(valueStr);
                            break;

                        case TypeCode.Int64:
                            value = long.Parse(valueStr);
                            break;

                        default:
                            if (typeof(T) == typeof(Guid))
                            {
                                value = Guid.Parse(valueStr);
                            }
                            else
                            {
                                value = Convert.ChangeType(valueStr, typeof(T));
                            }
                            break;
                    }

                    return (T)value;
                }
                catch
                {
                    throw new Exception($"获取 {key} 失败，{key} 类型转换失败");
                }
            }
            else
            {
                throw new Exception($"获取 {key} 失败，{key} 不存在");
            }
        }


    }
}
