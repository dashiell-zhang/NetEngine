using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace SourceGenerator.Runtime;

/// <summary>
/// 运行时缓存处理：封装 IDistributedCache 的读取与写入逻辑。
/// </summary>
internal static class CacheRuntime
{
    // JSON 处理已抽到 JsonUtil，便于其他 Runtime 复用。

    public static bool TryGet<T>(IServiceProvider? sp, SourceGenerator.Runtime.Options.CacheOptions cache, ILogger? logger, bool log, string method, out T value)
    {
        value = default!;
        var cacheSvc = sp?.GetService(typeof(IDistributedCache)) as IDistributedCache;
        if (cacheSvc is null) return false;
        try
        {
            var key = BuildKey(cache.Seed);
            var json = DistributedCacheExtensions.GetString(cacheSvc, key);
            if (json is null) return false;
            if (log) logger?.LogInformation($"Cache hit {method}");
            value = JsonSerializer.Deserialize<T>(json, JsonUtil.JsonOpts)!;
            return true;
        }
        catch (Exception ex)
        {
            if (log) logger?.LogInformation($"Cache read error {method}: {ex.Message}");
            return false;
        }
    }

    public static void Set<T>(IServiceProvider? sp, SourceGenerator.Runtime.Options.CacheOptions cache, ILogger? logger, bool log, string method, T value)
    {
        var cacheSvc = sp?.GetService(typeof(IDistributedCache)) as IDistributedCache;
        if (cacheSvc is null) return;
        try
        {
            var key = BuildKey(cache.Seed);
            var json = JsonSerializer.Serialize(value, JsonUtil.JsonOpts);
            DistributedCacheExtensions.SetString(cacheSvc, key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cache.TtlSeconds)
            });
            if (log) logger?.LogInformation($"Cache set {method} ttl={cache.TtlSeconds}");
        }
        catch (Exception ex)
        {
            if (log) logger?.LogInformation($"Cache write error {method}: {ex.Message}");
        }
    }

    public static async Task<(bool hit, T value)> TryGetAsync<T>(IServiceProvider? sp, SourceGenerator.Runtime.Options.CacheOptions cache, ILogger? logger, bool log, string method)
    {
        var cacheSvc = sp?.GetService(typeof(IDistributedCache)) as IDistributedCache;
        if (cacheSvc is null) return (false, default!);
        try
        {
            var key = BuildKey(cache.Seed);
            var json = await DistributedCacheExtensions.GetStringAsync(cacheSvc, key);
            if (json is null) return (false, default!);
            if (log) logger?.LogInformation($"Cache hit {method}");
            return (true, JsonSerializer.Deserialize<T>(json, JsonUtil.JsonOpts)!);
        }
        catch (Exception ex)
        {
            if (log) logger?.LogInformation($"Cache read error {method}: {ex.Message}");
            return (false, default!);
        }
    }

    public static async Task SetAsync<T>(IServiceProvider? sp, SourceGenerator.Runtime.Options.CacheOptions cache, ILogger? logger, bool log, string method, T value)
    {
        var cacheSvc = sp?.GetService(typeof(IDistributedCache)) as IDistributedCache;
        if (cacheSvc is null) return;
        try
        {
            var key = BuildKey(cache.Seed);
            var json = JsonSerializer.Serialize(value, JsonUtil.JsonOpts);
            await DistributedCacheExtensions.SetStringAsync(cacheSvc, key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cache.TtlSeconds)
            });
            if (log) logger?.LogInformation($"Cache set {method} ttl={cache.TtlSeconds}");
        }
        catch (Exception ex)
        {
            if (log) logger?.LogInformation($"Cache write error {method}: {ex.Message}");
        }
    }

    private static string BuildKey(string seed) => "CacheData_" + Md5Hex(seed);

    private static string Md5Hex(string s)
    {
        using var md5 = MD5.Create();
        var bytes = Encoding.UTF8.GetBytes(s);
        var hash = md5.ComputeHash(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
