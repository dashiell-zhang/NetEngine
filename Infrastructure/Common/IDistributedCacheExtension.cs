using Microsoft.Extensions.Caching.Distributed;

namespace Common;

/// <summary>
/// 扩展分布式缓存接口
/// </summary>
public static class IDistributedCacheExtension
{

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
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static async Task<bool> SetAsync(this IDistributedCache distributedCache, string key, string value, CancellationToken cancellationToken = default)
    {
        try
        {
            await distributedCache.SetStringAsync(key, value, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static async Task<bool> SetAsync(this IDistributedCache distributedCache, string key, string value, TimeSpan expirationRelativeToNow, bool isSlidingExp = false, CancellationToken cancellationToken = default)
    {
        try
        {
            await distributedCache.SetStringAsync(key, value, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = (isSlidingExp ? null : expirationRelativeToNow),
                SlidingExpiration = (isSlidingExp ? expirationRelativeToNow : null)
            }, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
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
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static async Task<bool> SetAsync(this IDistributedCache distributedCache, string key, string value, DateTimeOffset absoluteExpiration, CancellationToken cancellationToken = default)
    {
        try
        {
            await distributedCache.SetStringAsync(key, value, new DistributedCacheEntryOptions { AbsoluteExpiration = absoluteExpiration }, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
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
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static async Task<bool> SetAsync(this IDistributedCache distributedCache, string key, object value, CancellationToken cancellationToken = default)
    {
        try
        {
            var valueStr = JsonHelper.ObjectToJson(value);
            await distributedCache.SetStringAsync(key, valueStr, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
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
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static async Task<bool> SetAsync(this IDistributedCache distributedCache, string key, object value, TimeSpan expirationRelativeToNow, bool isSlidingExp = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var valueStr = JsonHelper.ObjectToJson(value);
            await distributedCache.SetStringAsync(key, valueStr, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = (isSlidingExp ? null : expirationRelativeToNow),
                SlidingExpiration = (isSlidingExp ? expirationRelativeToNow : null)
            }, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
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
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static async Task<bool> SetAsync(this IDistributedCache distributedCache, string key, object value, DateTimeOffset absoluteExpiration, CancellationToken cancellationToken = default)
    {
        try
        {
            var valueStr = JsonHelper.ObjectToJson(value);
            await distributedCache.SetStringAsync(key, valueStr, new DistributedCacheEntryOptions { AbsoluteExpiration = absoluteExpiration }, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }


    /// <summary>
    /// 获取 Object 类型的缓存
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <returns></returns>
    /// <remarks>缓存读取或反序列化失败时按未命中处理，避免缓存故障阻断业务</remarks>
    public static T? Get<T>(this IDistributedCache distributedCache, string key)
    {
        try
        {
            var valueStr = distributedCache.GetString(key);

            if (valueStr != null)
            {
                return JsonHelper.JsonToObject<T>(valueStr);
            }

            return default;
        }
        catch
        {
            return default;
        }
    }


    /// <summary>
    /// 获取 Object 类型的缓存（异步）
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    /// <remarks>缓存读取或反序列化失败时按未命中处理，用户主动取消仍向上传播</remarks>
    public static async Task<T?> GetAsync<T>(this IDistributedCache distributedCache, string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var valueStr = await distributedCache.GetStringAsync(key, cancellationToken);

            if (valueStr != null)
            {
                return JsonHelper.JsonToObject<T>(valueStr);
            }

            return default;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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
    /// <remarks>该实现通过读取缓存值判断，读取失败时返回 false，使用滑动过期时会刷新缓存有效期</remarks>
    public static bool IsContainKey(this IDistributedCache distributedCache, string key)
    {
        try
        {
            return distributedCache.GetString(key) != null;
        }
        catch
        {
            return false;
        }
    }



    /// <summary>
    /// 判断缓存是否存在（异步）
    /// </summary>
    /// <param name="key"></param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    /// <remarks>读取失败时返回 false，用户主动取消仍向上传播</remarks>
    public static async Task<bool> IsContainKeyAsync(this IDistributedCache distributedCache, string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return (await distributedCache.GetStringAsync(key, cancellationToken)) != null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

}
