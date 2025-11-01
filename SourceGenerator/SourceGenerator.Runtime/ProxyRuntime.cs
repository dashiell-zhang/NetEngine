using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace SourceGenerator.Runtime;

public static class ProxyRuntime
{
    public sealed class CacheOptions
    {
        public required string Seed { get; init; }
        public int TtlSeconds { get; init; }
    }

    // Sync (void)
    public static void Invoke(Action inner, string method, bool log, bool measure, string? args, IServiceProvider? sp)
    {
        var logger = (sp?.GetService(typeof(ILoggerFactory)) as ILoggerFactory)?.CreateLogger("SourceGenerator.Runtime.ProxyRuntime");
        if (log)
        {
            if (!string.IsNullOrEmpty(args)) logger?.Info($"[Proxy] Executing {method} args: {args}");
            else logger?.Info($"[Proxy] Executing {method}");
        }
        var sw = measure ? Stopwatch.StartNew() : null;
        inner();
        if (measure && sw is not null)
        {
            sw.Stop();
            logger?.Info($"[Proxy] Executed {method} in {sw.ElapsedMilliseconds}ms");
        }
    }

    // Sync (T)
    public static T Invoke<T>(Func<T> inner, string method, bool log, bool measure, string? args, IServiceProvider? sp, CacheOptions? cache)
    {
        var logger = (sp?.GetService(typeof(ILoggerFactory)) as ILoggerFactory)?.CreateLogger("SourceGenerator.Runtime.ProxyRuntime");
        IDistributedCache? cacheSvc = null;
        string? key = null;
        if (cache is not null && sp is not null)
        {
            cacheSvc = sp.GetService(typeof(IDistributedCache)) as IDistributedCache;
            key = BuildKey(cache.Seed);
            if (cacheSvc is not null)
            {
                try
                {
                    var json = DistributedCacheExtensions.GetString(cacheSvc, key);
                    if (json is not null)
                    {
                        if (log) logger?.Info($"[Proxy] Cache hit {method}");
                        return JsonSerializer.Deserialize<T>(json)!;
                    }
                }
                catch (Exception ex)
                {
                    if (log) logger?.Info($"[Proxy] Cache read error {method}: {ex.Message}");
                }
            }
        }

        if (log)
        {
            if (!string.IsNullOrEmpty(args)) logger?.Info($"[Proxy] Executing {method} args: {args}");
            else logger?.Info($"[Proxy] Executing {method}");
        }
        var sw = measure ? Stopwatch.StartNew() : null;
        var result = inner();
        if (measure && sw is not null)
        {
            sw.Stop();
            logger?.Info($"[Proxy] Executed {method} in {sw.ElapsedMilliseconds}ms");
        }
        if (log) logger?.Info($"[Proxy] Return={result}");

        if (cacheSvc is not null && key is not null)
        {
            try
            {
                var json = JsonSerializer.Serialize(result);
                DistributedCacheExtensions.SetString(cacheSvc, key, json, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cache!.TtlSeconds)
                });
                if (log) logger?.Info($"[Proxy] Cache set {method} ttl={cache!.TtlSeconds}");
            }
            catch (Exception ex)
            {
                if (log) logger?.Info($"[Proxy] Cache write error {method}: {ex.Message}");
            }
        }
        return result;
    }

    // Task (void)
    public static async Task InvokeTask(Func<Task> inner, string method, bool log, bool measure, string? args, IServiceProvider? sp)
    {
        var logger = (sp?.GetService(typeof(ILoggerFactory)) as ILoggerFactory)?.CreateLogger("SourceGenerator.Runtime.ProxyRuntime");
        if (log)
        {
            if (!string.IsNullOrEmpty(args)) logger?.Info($"[Proxy] Executing {method} args: {args}");
            else logger?.Info($"[Proxy] Executing {method}");
        }
        var sw = measure ? Stopwatch.StartNew() : null;
        await inner();
        if (measure && sw is not null)
        {
            sw.Stop();
            logger?.Info($"[Proxy] Executed {method} in {sw.ElapsedMilliseconds}ms");
        }
    }

    // Task<T>
    public static async Task<T> InvokeTask<T>(Func<Task<T>> inner, string method, bool log, bool measure, string? args, IServiceProvider? sp, CacheOptions? cache)
    {
        var logger = (sp?.GetService(typeof(ILoggerFactory)) as ILoggerFactory)?.CreateLogger("SourceGenerator.Runtime.ProxyRuntime");
        IDistributedCache? cacheSvc = null;
        string? key = null;
        if (cache is not null && sp is not null)
        {
            cacheSvc = sp.GetService(typeof(IDistributedCache)) as IDistributedCache;
            key = BuildKey(cache.Seed);
            if (cacheSvc is not null)
            {
                try
                {
                    var json = await DistributedCacheExtensions.GetStringAsync(cacheSvc, key);
                    if (json is not null)
                    {
                        if (log) logger?.Info($"[Proxy] Cache hit {method}");
                        return JsonSerializer.Deserialize<T>(json)!;
                    }
                }
                catch (Exception ex)
                {
                    if (log) logger?.Info($"[Proxy] Cache read error {method}: {ex.Message}");
                }
            }
        }

        if (log)
        {
            if (!string.IsNullOrEmpty(args)) logger?.Info($"[Proxy] Executing {method} args: {args}");
            else logger?.Info($"[Proxy] Executing {method}");
        }
        var sw = measure ? Stopwatch.StartNew() : null;
        var result = await inner();
        if (measure && sw is not null)
        {
            sw.Stop();
            logger?.Info($"[Proxy] Executed {method} in {sw.ElapsedMilliseconds}ms");
        }
        if (log) logger?.Info($"[Proxy] Return={result}");

        if (cacheSvc is not null && key is not null)
        {
            try
            {
                var json = JsonSerializer.Serialize(result);
                await DistributedCacheExtensions.SetStringAsync(cacheSvc, key, json, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cache!.TtlSeconds)
                });
                if (log) logger?.Info($"[Proxy] Cache set {method} ttl={cache!.TtlSeconds}");
            }
            catch (Exception ex)
            {
                if (log) logger?.Info($"[Proxy] Cache write error {method}: {ex.Message}");
            }
        }
        return result;
    }

    // ValueTask (void)
    public static async ValueTask InvokeValueTask(Func<ValueTask> inner, string method, bool log, bool measure, string? args, IServiceProvider? sp)
    {
        var logger = (sp?.GetService(typeof(ILoggerFactory)) as ILoggerFactory)?.CreateLogger("SourceGenerator.Runtime.ProxyRuntime");
        if (log)
        {
            if (!string.IsNullOrEmpty(args)) logger?.Info($"[Proxy] Executing {method} args: {args}");
            else logger?.Info($"[Proxy] Executing {method}");
        }
        var sw = measure ? Stopwatch.StartNew() : null;
        await inner();
        if (measure && sw is not null)
        {
            sw.Stop();
            logger?.Info($"[Proxy] Executed {method} in {sw.ElapsedMilliseconds}ms");
        }
    }

    // ValueTask<T>
    public static async ValueTask<T> InvokeValueTask<T>(Func<ValueTask<T>> inner, string method, bool log, bool measure, string? args, IServiceProvider? sp, CacheOptions? cache)
    {
        var logger = (sp?.GetService(typeof(ILoggerFactory)) as ILoggerFactory)?.CreateLogger("SourceGenerator.Runtime.ProxyRuntime");
        IDistributedCache? cacheSvc = null;
        string? key = null;
        if (cache is not null && sp is not null)
        {
            cacheSvc = sp.GetService(typeof(IDistributedCache)) as IDistributedCache;
            key = BuildKey(cache.Seed);
            if (cacheSvc is not null)
            {
                try
                {
                    var json = await DistributedCacheExtensions.GetStringAsync(cacheSvc, key);
                    if (json is not null)
                    {
                        if (log) logger?.Info($"[Proxy] Cache hit {method}");
                        return JsonSerializer.Deserialize<T>(json)!;
                    }
                }
                catch (Exception ex)
                {
                    if (log) logger?.Info($"[Proxy] Cache read error {method}: {ex.Message}");
                }
            }
        }

        if (log)
        {
            if (!string.IsNullOrEmpty(args)) logger?.Info($"[Proxy] Executing {method} args: {args}");
            else logger?.Info($"[Proxy] Executing {method}");
        }
        var sw = measure ? Stopwatch.StartNew() : null;
        var result = await inner();
        if (measure && sw is not null)
        {
            sw.Stop();
            logger?.Info($"[Proxy] Executed {method} in {sw.ElapsedMilliseconds}ms");
        }
        if (log) logger?.Info($"[Proxy] Return={result}");

        if (cacheSvc is not null && key is not null)
        {
            try
            {
                var json = JsonSerializer.Serialize(result);
                await DistributedCacheExtensions.SetStringAsync(cacheSvc, key, json, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cache!.TtlSeconds)
                });
                if (log) logger?.Info($"[Proxy] Cache set {method} ttl={cache!.TtlSeconds}");
            }
            catch (Exception ex)
            {
                if (log) logger?.Info($"[Proxy] Cache write error {method}: {ex.Message}");
            }
        }
        return result;
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

