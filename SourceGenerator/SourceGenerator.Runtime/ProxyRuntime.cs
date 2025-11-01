using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace SourceGenerator.Runtime;

/// <summary>
/// 代理运行时工具：统一封装日志、计时与可选的分布式缓存。
/// - 日志：通过 <see cref="ILoggerFactory"/> 从 <see cref="IServiceProvider"/> 解析，分类名为 "SourceGenerator.Runtime.ProxyRuntime"。
/// - 计时：当 <paramref name="measure"/> 为 true 时使用 <see cref="Stopwatch"/> 记录耗时。
/// - 参数：当生成端开启参数捕获时，会以字符串形式传入 <paramref name="args"/> 并输出到日志。
/// - 缓存：当提供 <see cref="CacheOptions"/> 且容器中存在 <see cref="IDistributedCache"/> 时，按 Seed 生成键读写缓存。
/// 注意：所有方法均不吞异常，异常会原样抛出，由调用方处理。
/// </summary>
public static class ProxyRuntime
{
    /// <summary>
    /// 缓存参数。
    /// </summary>
    public sealed class CacheOptions
    {
        /// <summary>
        /// 参与生成缓存键的种子（通常由 生成端 传入“方法标识 + 参数摘要”）。
        /// </summary>
        public required string Seed { get; init; }

        /// <summary>
        /// 缓存有效期（秒）。
        /// </summary>
        public int TtlSeconds { get; init; }
    }

    /// <summary>
    /// 同步无返回值调用。
    /// 记录开始日志（含参数，可选）、可选计时并记录完成日志。
    /// </summary>
    /// <param name="inner">实际执行的委托。</param>
    /// <param name="method">方法全名（用于日志）。</param>
    /// <param name="log">是否输出日志。</param>
    /// <param name="measure">是否统计耗时。</param>
    /// <param name="args">参数字符串（可为空）。</param>
    /// <param name="sp">服务提供者，用于解析 <see cref="ILoggerFactory"/>。</param>
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

    /// <summary>
    /// 同步有返回值调用，带可选分布式缓存。
    /// 命中缓存时直接返回并记录命中日志；未命中则执行业务、记录日志并尝试写入缓存。
    /// </summary>
    /// <typeparam name="T">返回值类型。</typeparam>
    /// <param name="inner">实际执行的委托。</param>
    /// <param name="method">方法全名（用于日志）。</param>
    /// <param name="log">是否输出日志。</param>
    /// <param name="measure">是否统计耗时。</param>
    /// <param name="args">参数字符串（可为空）。</param>
    /// <param name="sp">服务提供者，用于解析 <see cref="ILoggerFactory"/> 与 <see cref="IDistributedCache"/>。</param>
    /// <param name="cache">缓存选项；null 表示不启用缓存。</param>
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
        if (log) logger?.Info($"[Proxy] Return {method} = {result}");

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

    /// <summary>
    /// 异步无返回值调用（Task）。
    /// 记录开始日志（含参数，可选）、可选计时并记录完成日志。
    /// </summary>
    /// <param name="inner">实际执行的异步委托。</param>
    /// <param name="method">方法全名（用于日志）。</param>
    /// <param name="log">是否输出日志。</param>
    /// <param name="measure">是否统计耗时。</param>
    /// <param name="args">参数字符串（可为空）。</param>
    /// <param name="sp">服务提供者，用于解析 <see cref="ILoggerFactory"/>。</param>
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

    /// <summary>
    /// 异步有返回值调用（Task&lt;T&gt;），带可选分布式缓存。
    /// 命中缓存时直接返回并记录命中日志；未命中则执行业务、记录日志并尝试写入缓存。
    /// </summary>
    /// <typeparam name="T">返回值类型。</typeparam>
    /// <param name="inner">实际执行的异步委托。</param>
    /// <param name="method">方法全名（用于日志）。</param>
    /// <param name="log">是否输出日志。</param>
    /// <param name="measure">是否统计耗时。</param>
    /// <param name="args">参数字符串（可为空）。</param>
    /// <param name="sp">服务提供者，用于解析 <see cref="ILoggerFactory"/> 与 <see cref="IDistributedCache"/>。</param>
    /// <param name="cache">缓存选项；null 表示不启用缓存。</param>
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
        if (log) logger?.Info($"[Proxy] Return {method} = {result}");

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

    /// <summary>
    /// 异步无返回值调用（ValueTask）。
    /// 记录开始日志（含参数，可选）、可选计时并记录完成日志。
    /// </summary>
    /// <param name="inner">实际执行的异步委托。</param>
    /// <param name="method">方法全名（用于日志）。</param>
    /// <param name="log">是否输出日志。</param>
    /// <param name="measure">是否统计耗时。</param>
    /// <param name="args">参数字符串（可为空）。</param>
    /// <param name="sp">服务提供者，用于解析 <see cref="ILoggerFactory"/>。</param>
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

    /// <summary>
    /// 异步有返回值调用（ValueTask&lt;T&gt;），带可选分布式缓存。
    /// 命中缓存时直接返回并记录命中日志；未命中则执行业务、记录日志并尝试写入缓存。
    /// </summary>
    /// <typeparam name="T">返回值类型。</typeparam>
    /// <param name="inner">实际执行的异步委托。</param>
    /// <param name="method">方法全名（用于日志）。</param>
    /// <param name="log">是否输出日志。</param>
    /// <param name="measure">是否统计耗时。</param>
    /// <param name="args">参数字符串（可为空）。</param>
    /// <param name="sp">服务提供者，用于解析 <see cref="ILoggerFactory"/> 与 <see cref="IDistributedCache"/>。</param>
    /// <param name="cache">缓存选项；null 表示不启用缓存。</param>
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
        if (log) logger?.Info($"[Proxy] Return {method} = {result}");

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

    /// <summary>
    /// 生成分布式缓存键：前缀 <c>CacheData_</c> + <see cref="Md5Hex(string)"/>。
    /// </summary>
    /// <param name="seed">原始种子字符串。</param>
    /// <returns>缓存键。</returns>
    private static string BuildKey(string seed) => "CacheData_" + Md5Hex(seed);
    /// <summary>
    /// 计算字符串的 MD5 小写十六进制摘要（UTF-8）。
    /// </summary>
    /// <param name="s">输入字符串。</param>
    /// <returns>32 位小写十六进制哈希。</returns>
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

