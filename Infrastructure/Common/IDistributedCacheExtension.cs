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
        /// <returns></returns>
        public static bool Set(this IDistributedCache distributedCache, string key, string value)
        {
            try
            {
                distributedCache.SetString(key, value);
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
        /// <returns></returns>
        public static void SetAsync(this IDistributedCache distributedCache, string key, string value)
        {
            Task.Run(() =>
            {
                distributedCache.Set(key, value);
            });
        }



        /// <summary>
        /// 设置 string 类型的缓存
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expirationRelativeToNow">相对过期时间</param>
        /// <param name="isSlidingExp">是否支持滑动延时</param>
        /// <returns></returns>
        public static bool Set(this IDistributedCache distributedCache, string key, string value, TimeSpan expirationRelativeToNow, bool isSlidingExp = false)
        {
            try
            {
                distributedCache.SetString(key, value, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = (isSlidingExp ? null : expirationRelativeToNow),
                    SlidingExpiration = (isSlidingExp ? expirationRelativeToNow : null)
                });
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
        /// <param name="expirationRelativeToNow">相对过期时间</param>
        /// <param name="isSlidingExp">是否支持滑动延时</param>
        /// <returns></returns>
        public static void SetAsync(this IDistributedCache distributedCache, string key, string value, TimeSpan expirationRelativeToNow, bool isSlidingExp = false)
        {
            Task.Run(() =>
            {
                distributedCache.Set(key, value, expirationRelativeToNow, isSlidingExp);
            });
        }




        /// <summary>
        /// 设置 string 类型的缓存
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        /// <param name="absoluteExpiration">绝对过期时间</param>
        /// <returns></returns>
        public static bool Set(this IDistributedCache distributedCache, string key, string value, DateTimeOffset absoluteExpiration)
        {
            try
            {
                distributedCache.SetString(key, value, new DistributedCacheEntryOptions { AbsoluteExpiration = absoluteExpiration });
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
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        /// <param name="absoluteExpiration">绝对过期时间</param>
        /// <returns></returns>
        public static void SetAsync(this IDistributedCache distributedCache, string key, string value, DateTimeOffset absoluteExpiration)
        {
            Task.Run(() =>
            {
                distributedCache.Set(key, value, absoluteExpiration);
            });
        }




        /// <summary>
        /// 设置 object 类型的缓存
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool Set(this IDistributedCache distributedCache, string key, object value)
        {
            try
            {
                var valueStr = JsonHelper.ObjectToJson(value);
                distributedCache.SetString(key, valueStr);
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
        /// <returns></returns>
        public static void SetAsync(this IDistributedCache distributedCache, string key, object value)
        {
            Task.Run(() =>
            {
                distributedCache.Set(key, value);
            });
        }



        /// <summary>
        /// 设置 object 类型的缓存
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expirationRelativeToNow">相对过期时间</param>
        /// <param name="isSlidingExp">是否支持滑动延时</param>
        /// <returns></returns>
        public static bool Set(this IDistributedCache distributedCache, string key, object value, TimeSpan expirationRelativeToNow, bool isSlidingExp = false)
        {
            try
            {
                var valueStr = JsonHelper.ObjectToJson(value);
                distributedCache.SetString(key, valueStr, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = (isSlidingExp ? null : expirationRelativeToNow),
                    SlidingExpiration = (isSlidingExp ? expirationRelativeToNow : null)
                });
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
        /// <param name="expirationRelativeToNow">相对过期时间</param>
        /// <param name="isSlidingExp">是否支持滑动延时</param>
        /// <returns></returns>
        public static void SetAsync(this IDistributedCache distributedCache, string key, object value, TimeSpan expirationRelativeToNow, bool isSlidingExp = false)
        {
            Task.Run(() =>
            {
                distributedCache.Set(key, value, expirationRelativeToNow, isSlidingExp);
            });
        }



        /// <summary>
        /// 设置 object 类型的缓存
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="absoluteExpiration">绝对过期时间</param>
        /// <returns></returns>
        public static bool Set(this IDistributedCache distributedCache, string key, object value, DateTimeOffset absoluteExpiration)
        {
            try
            {
                var valueStr = JsonHelper.ObjectToJson(value);
                distributedCache.SetString(key, valueStr, new DistributedCacheEntryOptions { AbsoluteExpiration = absoluteExpiration });
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
        /// <param name="absoluteExpiration">绝对过期时间</param>
        /// <returns></returns>
        public static void SetAsync(this IDistributedCache distributedCache, string key, object value, DateTimeOffset absoluteExpiration)
        {
            Task.Run(() =>
            {
                distributedCache.Set(key, value, absoluteExpiration);
            });
        }




        /// <summary>
        /// 获取 Object 类型的缓存
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public static T? Get<T>(this IDistributedCache distributedCache, string key)
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
                    return default;
                }
            }
            catch
            {
                return default;
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
