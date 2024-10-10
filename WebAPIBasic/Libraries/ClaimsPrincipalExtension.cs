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

                var type = typeof(T);

                if (typeof(T) == typeof(Guid) || typeof(T) == typeof(Guid?))
                {
                    value = Guid.Parse(valueStr);
                }
                else if (typeof(T) == typeof(long) || typeof(T) == typeof(long?))
                {
                    value = long.Parse(valueStr);
                }
                else if (typeof(T) == typeof(string))
                {
                    value = valueStr;
                }
                else if (typeof(T) == typeof(DateTime) || typeof(T) == typeof(DateTime?))
                {
                    value = DateTime.Parse(valueStr);
                }
                else if (typeof(T) == typeof(DateTimeOffset) || typeof(T) == typeof(DateTimeOffset?))
                {
                    value = DateTimeOffset.Parse(valueStr);
                }
                else if (typeof(T) == typeof(int) || typeof(T) == typeof(int?))
                {
                    value = int.Parse(valueStr);
                }
                else if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?))
                {
                    value = bool.Parse(valueStr);
                }
                else if (typeof(T) == typeof(decimal) || typeof(T) == typeof(decimal?))
                {
                    value = decimal.Parse(valueStr);
                }
                else
                {
                    if (IsNullable(type))
                    {
                        var parentType = type.GenericTypeArguments[0];
                        value = Convert.ChangeType(valueStr, parentType);
                    }
                    else
                    {
                        value = Convert.ChangeType(valueStr, typeof(T));
                    }
                }

                return (T)value;
            }
            catch
            {
                throw new Exception($"获取 {key} 失败，{key} 类型转换失败");
            }


            bool IsNullable(Type type)
            {
                bool isNullable = false;

                if (Nullable.GetUnderlyingType(type) != null)
                {
                    isNullable = true;
                }
                else if (!type.IsValueType)
                {
                    isNullable = true;
                }

                return isNullable;
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
