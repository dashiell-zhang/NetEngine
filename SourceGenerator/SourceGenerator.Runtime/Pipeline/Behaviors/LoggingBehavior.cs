using System;
using System.Diagnostics;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace SourceGenerator.Runtime;

/// <summary>
/// 记录调用生命周期：开始、耗时及返回值（如果有）。
/// 日志详细程度由 Logger 配置控制。
/// </summary>
public sealed class LoggingBehavior : IInvocationBehavior
{
    private static string[] BuildCallerChainArray(int maxDepth = 100)
    {
        try
        {
            var st = new StackTrace(skipFrames: 1, fNeedFileInfo: true);
            var frames = st.GetFrames();
            if (frames is null || frames.Length == 0) return Array.Empty<string>();

            var parts = new List<string>(8);
            foreach (var frame in frames)
            {
                var method = frame.GetMethod();
                if (method is null) continue;
                var typeName = method.DeclaringType?.FullName ?? "<global>";

                // Skip internal/framework/runtime frames
                if (typeName.StartsWith("SourceGenerator.Runtime")) continue;
                if (typeName.StartsWith("System.")) continue;
                if (typeName.StartsWith("Microsoft.")) continue;
                if (typeName.StartsWith("Swashbuckle.")) continue;
                if (typeName.StartsWith("WebAPI.Core.")) continue;
                if (typeName.StartsWith("<global>")) continue;
                if (method.DeclaringType?.Name.EndsWith("_Proxy") == true) continue;


                var name = typeName + "." + method.Name;
                var file = frame.GetFileName();
                if (!string.IsNullOrEmpty(file))
                {
                    var line = frame.GetFileLineNumber();
                    name += $" ({System.IO.Path.GetFileName(file)}:{line})";
                }
                parts.Add(name);
            }
            if (parts.Count == 0) return Array.Empty<string>();
            if (parts.Count > maxDepth)
            {
                parts = parts.GetRange(parts.Count - maxDepth, maxDepth);
            }
            parts.Reverse(); // oldest (entry) first
            return parts.ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public async ValueTask<T> InvokeAsync<T>(InvocationContext ctx, Func<ValueTask<T>> next)
    {
        var logger = ctx.Logger;

        // 如果启用了耗时测量，则开始计时
        Stopwatch? sw = ctx.Measure ? Stopwatch.StartNew() : null;

        // 调用开始：如果启用日志，则记录方法名称与参数
        string[] callerChain = Array.Empty<string>();
        if (ctx.Log)
        {
            callerChain = BuildCallerChainArray();
            var hasArgs = !string.IsNullOrEmpty(ctx.ArgsJson);
            var payload = new Dictionary<string, object?>
            {
                ["event"] = "executing",
                ["method"] = ctx.Method,
            };
            if (hasArgs)
                payload["args"] = ctx.ArgsJson; // 目前生成器提供的是字符串形式
            if (callerChain.Length > 0)
                payload["caller"] = callerChain;
            logger?.LogInformation(JsonUtil.ToLogJson(payload));
        }

        // (耗时测量已在上方按需开始)

        var result = await next();

        // 调用结束：如果启用日志和耗时统计，则记录执行时间
        if (ctx.Log && ctx.Measure && sw is not null)
        {
            sw.Stop();
            var payload = new Dictionary<string, object?>
            {
                ["event"] = "executed",
                ["method"] = ctx.Method,
                ["duration_ms"] = sw.ElapsedMilliseconds,
            };
            if (callerChain.Length > 0)
                payload["caller"] = callerChain;
            logger?.LogInformation(JsonUtil.ToLogJson(payload));
        }

        // 返回结果：如果启用日志且返回类型非 Unit，则序列化输出
        if (ctx.Log && typeof(T) != typeof(Unit))
        {
            var json = JsonUtil.ToJson(result);
            var payload = new Dictionary<string, object?>
            {
                ["event"] = "return",
                ["method"] = ctx.Method,
                ["result"] = result,
            };
            if (callerChain.Length > 0)
                payload["caller"] = callerChain;
            logger?.LogInformation(JsonUtil.ToLogJson(payload));
        }

        return result;
    }
}
