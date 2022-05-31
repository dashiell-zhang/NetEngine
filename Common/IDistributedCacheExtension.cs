using Microsoft.Extensions.Caching.Distributed;
using System;

namespace Common
{
    public static class IDistributedCacheExtension
    {


        /// <summary>
        /// 删除指定key
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
        /// 设置object类型的key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool SetObject(this IDistributedCache distributedCache, string key, object value)
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
        /// 设置string类型key,包含有效时间
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static bool SetString(this IDistributedCache distributedCache, string key, string value, TimeSpan timeOut)
        {
            try
            {
                distributedCache.SetString(key, value, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = timeOut });
                return true;
            }
            catch
            {
                return false;
            }
        }



        /// <summary>
        /// 设置object类型key,包含有效时间
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static bool SetObject(this IDistributedCache distributedCache, string key, object value, TimeSpan timeOut)
        {
            try
            {
                var valueStr = JsonHelper.ObjectToJson(value);
                distributedCache.SetString(key, valueStr, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = timeOut });
                return true;
            }
            catch
            {
                return false;
            }
        }




        /// <summary>
        /// 读取 Object 类型的key
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public static T GetObject<T>(this IDistributedCache distributedCache, string key)
        {
            try
            {
                var valueStr = distributedCache.GetString(key);
                var value = JsonHelper.JsonToObject<T>(valueStr);
                return value;
            }
            catch
            {
                return default!;
            }
        }



        /// <summary>
        /// 判断是否存在指定key
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
