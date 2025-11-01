using System.Diagnostics;
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
    public static void Invoke(Action inner, string method, bool log, bool measure, string? args, IServiceProvider? sp)
    {
        var logger = (sp?.GetService(typeof(ILoggerFactory)) as ILoggerFactory)?.CreateLogger("SourceGenerator.Runtime.ProxyRuntime");
        if (log)
        {
            if (!string.IsNullOrEmpty(args)) logger?.LogInformation($"[Proxy] Executing {method} args: {args}");
            else logger?.LogInformation($"[Proxy] Executing {method}");
        }
        var sw = measure ? Stopwatch.StartNew() : null;
        inner();
        if (measure && sw is not null)
        {
            sw.Stop();
            logger?.LogInformation($"[Proxy] Executed {method} in {sw.ElapsedMilliseconds}ms");
        }
    }

    /// <summary>
    /// 同步有返回值调用，带可选分布式缓存。
    /// </summary>
    public static T Invoke<T>(Func<T> inner, string method, bool log, bool measure, string? args, IServiceProvider? sp, CacheOptions? cache)
    {
        var logger = (sp?.GetService(typeof(ILoggerFactory)) as ILoggerFactory)?.CreateLogger("SourceGenerator.Runtime.ProxyRuntime");
        if (cache is not null)
        {
            if (CacheRuntime.TryGet<T>(sp, cache, logger, log, method, out var cached)) return cached;
        }
        if (log)
        {
            if (!string.IsNullOrEmpty(args)) logger?.LogInformation($"[Proxy] Executing {method} args: {args}");
            else logger?.LogInformation($"[Proxy] Executing {method}");
        }
        var sw = measure ? Stopwatch.StartNew() : null;
        var result = inner();
        if (measure && sw is not null)
        {
            sw.Stop();
            logger?.LogInformation($"[Proxy] Executed {method} in {sw.ElapsedMilliseconds}ms");
        }
        if (log)
        {
            var __json = JsonUtil.ToJson(result);
            logger?.LogInformation($"[Proxy] Return {method} = {__json}");
        }
        if (cache is not null)
        {
            CacheRuntime.Set(sp, cache, logger, log, method, result);
        }
        return result;
    }

    /// <summary>
    /// 异步无返回值调用（Task）。
    /// </summary>
    public static async Task InvokeTask(Func<Task> inner, string method, bool log, bool measure, string? args, IServiceProvider? sp)
    {
        var logger = (sp?.GetService(typeof(ILoggerFactory)) as ILoggerFactory)?.CreateLogger("SourceGenerator.Runtime.ProxyRuntime");
        if (log)
        {
            if (!string.IsNullOrEmpty(args)) logger?.LogInformation($"[Proxy] Executing {method} args: {args}");
            else logger?.LogInformation($"[Proxy] Executing {method}");
        }
        var sw = measure ? Stopwatch.StartNew() : null;
        await inner();
        if (measure && sw is not null)
        {
            sw.Stop();
            logger?.LogInformation($"[Proxy] Executed {method} in {sw.ElapsedMilliseconds}ms");
        }
    }

    /// <summary>
    /// 异步有返回值调用（Task&lt;T&gt;），带可选分布式缓存。
    /// </summary>
    public static async Task<T> InvokeTask<T>(Func<Task<T>> inner, string method, bool log, bool measure, string? args, IServiceProvider? sp, CacheOptions? cache)
    {
        var logger = (sp?.GetService(typeof(ILoggerFactory)) as ILoggerFactory)?.CreateLogger("SourceGenerator.Runtime.ProxyRuntime");
        if (cache is not null)
        {
            var get = await CacheRuntime.TryGetAsync<T>(sp, cache, logger, log, method);
            if (get.hit) return get.value;
        }
        if (log)
        {
            if (!string.IsNullOrEmpty(args)) logger?.LogInformation($"[Proxy] Executing {method} args: {args}");
            else logger?.LogInformation($"[Proxy] Executing {method}");
        }
        var sw = measure ? Stopwatch.StartNew() : null;
        var result = await inner();
        if (measure && sw is not null)
        {
            sw.Stop();
            logger?.LogInformation($"[Proxy] Executed {method} in {sw.ElapsedMilliseconds}ms");
        }
        if (log)
        {
            var __json = JsonUtil.ToJson(result);
            logger?.LogInformation($"[Proxy] Return {method} = {__json}");
        }
        if (cache is not null)
        {
            await CacheRuntime.SetAsync(sp, cache, logger, log, method, result);
        }
        return result;
    }

    /// <summary>
    /// 异步无返回值调用（ValueTask）。
    /// </summary>
    public static async ValueTask InvokeValueTask(Func<ValueTask> inner, string method, bool log, bool measure, string? args, IServiceProvider? sp)
    {
        var logger = (sp?.GetService(typeof(ILoggerFactory)) as ILoggerFactory)?.CreateLogger("SourceGenerator.Runtime.ProxyRuntime");
        if (log)
        {
            if (!string.IsNullOrEmpty(args)) logger?.LogInformation($"[Proxy] Executing {method} args: {args}");
            else logger?.LogInformation($"[Proxy] Executing {method}");
        }
        var sw = measure ? Stopwatch.StartNew() : null;
        await inner();
        if (measure && sw is not null)
        {
            sw.Stop();
            logger?.LogInformation($"[Proxy] Executed {method} in {sw.ElapsedMilliseconds}ms");
        }
    }

    /// <summary>
    /// 异步有返回值调用（ValueTask&lt;T&gt;），带可选分布式缓存。
    /// </summary>
    public static async ValueTask<T> InvokeValueTask<T>(Func<ValueTask<T>> inner, string method, bool log, bool measure, string? args, IServiceProvider? sp, CacheOptions? cache)
    {
        var logger = (sp?.GetService(typeof(ILoggerFactory)) as ILoggerFactory)?.CreateLogger("SourceGenerator.Runtime.ProxyRuntime");
        if (cache is not null)
        {
            var get = await CacheRuntime.TryGetAsync<T>(sp, cache, logger, log, method);
            if (get.hit) return get.value;
        }
        if (log)
        {
            if (!string.IsNullOrEmpty(args)) logger?.LogInformation($"[Proxy] Executing {method} args: {args}");
            else logger?.LogInformation($"[Proxy] Executing {method}");
        }
        var sw = measure ? Stopwatch.StartNew() : null;
        var result = await inner();
        if (measure && sw is not null)
        {
            sw.Stop();
            logger?.LogInformation($"[Proxy] Executed {method} in {sw.ElapsedMilliseconds}ms");
        }
        if (log)
        {
            var __json = JsonUtil.ToJson(result);
            logger?.LogInformation($"[Proxy] Return {method} = {__json}");
        }
        if (cache is not null)
        {
            await CacheRuntime.SetAsync(sp, cache, logger, log, method, result);
        }
        return result;
    }
}
