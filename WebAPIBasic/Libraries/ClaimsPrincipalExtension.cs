using System.Security.Claims;

namespace WebAPIBasic.Libraries
{
    public static class ClaimsPrincipalExtension
    {


        private static T AccelerateConvert<T>(string key, string valueStr)
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

                    case TypeCode.Decimal:
                        value = decimal.Parse(valueStr);
                        break;

                    case TypeCode.Int32:
                        value = int.Parse(valueStr);
                        break;

                    case TypeCode.Int64:
                        value = long.Parse(valueStr);
                        break;

                    case TypeCode.DateTime:
                        value = DateTime.Parse(valueStr);
                        break;

                    default:
                        if (typeof(T) == typeof(Guid))
                        {
                            value = Guid.Parse(valueStr);
                        }
                        else if (typeof(T) == typeof(DateTimeOffset))
                        {
                            value = DateTimeOffset.Parse(valueStr);
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


        /// <summary>
        /// 通过Key获取Claims中的信息
        /// </summary>
        /// <param name="claims"></param>
        /// <param name="key">Claim关键字</param>
        /// <returns></returns>
        public static T GetClaim<T>(this ClaimsPrincipal claims, string key)
        {
            var valueStr = claims.FindFirst(key)?.Value;

            if (valueStr != null)
            {
                return AccelerateConvert<T>(key, valueStr);
            }
            else
            {
                throw new Exception($"获取 {key} 失败，{key} 不存在");
            }
        }



        /// <summary>
        /// 通过Key获取Claims中的信息,不存在则返回 T 默认值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="claims"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static T GetClaimOrDefault<T>(this ClaimsPrincipal claims, string key)
        {
            var valueStr = claims.FindFirst(key)?.Value;

            if (valueStr != null)
            {
                return AccelerateConvert<T>(key, valueStr);
            }
            else
            {
                return default!;
            }
        }



        /// <summary>
        /// 尝试通过Key获取Claims中的信息
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="claims"></param>
        /// <param name="key"></param>
        /// <param name="retValue"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static bool TryGetClaim<T>(this ClaimsPrincipal claims, string key, out T retValue)
        {
            var valueStr = claims.FindFirst(key)?.Value;

            if (valueStr != null)
            {
                retValue = AccelerateConvert<T>(key, valueStr);

                return true;
            }
            else
            {
                retValue = default!;
                return false;
            }
        }

    }
}
