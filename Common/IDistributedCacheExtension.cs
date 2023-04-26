using Microsoft.Extensions.Caching.Distributed;

namespace Common
{

    /// <summary>
    /// 扩展分布式缓存接口
    /// </summary>
    /// </summary>
    public static class IDistributedCacheExtension
    {


        /// <summary>
        /// 删除缓存
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static bool Remove(this IDistributedCache distributedCache, string key)
        {
            try
            {
                distributedCache.Remove(key);
                return true;
            }
            catch
            {
                return false;
            }
        }



        /// <summary>
        /// 设置 string 类型的缓存
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static bool SetString(this IDistributedCache distributedCache, string key, string value, TimeSpan? timeOut = null)
        {
            try
            {
                if (timeOut == null)
                {
                    distributedCache.SetString(key, value, timeOut);
                }
                else
                {
                    distributedCache.SetString(key, value, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = timeOut });
                }

                return true;
            }
            catch
            {
                return false;
            }
        }



        /// <summary>
        /// 设置 string 类型的缓存（异步）
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static void SetStringAsync(this IDistributedCache distributedCache, string key, string value, TimeSpan? timeOut = null)
        {
            Task.Run(() =>
            {
                distributedCache.SetString(key, value, timeOut);
            });
        }





        /// <summary>
        /// 设置 object 类型的缓存
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static bool SetObject(this IDistributedCache distributedCache, string key, object value, TimeSpan? timeOut = null)
        {
            try
            {
                var valueStr = JsonHelper.ObjectToJson(value);

                if (timeOut == null)
                {
                    distributedCache.SetString(key, valueStr);
                }
                else
                {
                    distributedCache.SetString(key, valueStr, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = timeOut });
                }

                return true;
            }
            catch
            {
                return false;
            }
        }



        /// <summary>
        /// 设置 object 类型的缓存（异步）
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static void SetObjectAsync(this IDistributedCache distributedCache, string key, object value, TimeSpan? timeOut = null)
        {
            Task.Run(() =>
            {
                distributedCache.SetObject(key, value, timeOut);
            });
        }




        /// <summary>
        /// 获取 Object 类型的缓存
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public static T GetObject<T>(this IDistributedCache distributedCache, string key)
        {
            try
            {
                var valueStr = distributedCache.GetString(key);

                if (valueStr != null)
                {
                    var value = JsonHelper.JsonToObject<T>(valueStr);
                    return value;
                }
                else
                {
                    return default!;
                }
            }
            catch
            {
                return default!;
            }
        }



        /// <summary>
        /// 判断缓存是否存在
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static bool IsContainKey(this IDistributedCache distributedCache, string key)
        {
            if (string.IsNullOrEmpty(distributedCache.GetString(key)))
            {
                return false;
            }
            else
            {
                return true;
            }
        }


    }
}
