using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace SourceGenerator.Runtime.Pipeline.Behaviors;

/// <summary>
/// 在调用前后和异常时记录结构化日志 支持异步和同步行为接口
/// </summary>
public sealed class LoggingBehavior : IInvocationAsyncBehavior, IInvocationBehavior
{

    /// <summary>
    /// 用于保存同步行为计时信息的内部状态
    /// </summary>
    private sealed class LoggingState
    {
        public long StartTicks { get; set; }
    }

    /// <summary>
    /// 构建调用栈中业务相关调用方链路的字符串数组
    /// </summary>
    private static string[] BuildCallerChainArray(int maxDepth = 100)
    {
        try
        {
            var st = new StackTrace(skipFrames: 1, fNeedFileInfo: false);
            
            var frames = st.GetFrames();
            
            if (frames is null || frames.Length == 0) return Array.Empty<string>();

            var parts = new List<string>(8);
            
            foreach (var frame in frames)
            {
                var method = frame.GetMethod();
                
                if (method is null) continue;
                
                var typeName = method.DeclaringType?.FullName ?? "<global>";

                if (typeName.StartsWith("SourceGenerator.Runtime")) continue;
                
                if (typeName.StartsWith("System.")) continue;
                
                if (typeName.StartsWith("Microsoft.")) continue;
                
                if (typeName.StartsWith("Swashbuckle.")) continue;
                
                if (typeName.StartsWith("WebAPI.Core.")) continue;
                
                if (typeName.StartsWith("Npgsql.")) continue;
                
                if (typeName.StartsWith("<global>")) continue;
                
                if (method.DeclaringType?.Name.EndsWith("_Proxy") == true) continue;

                parts.Add(typeName + "." + method.Name);
            }
            
            if (parts.Count == 0) return Array.Empty<string>();
            
            if (parts.Count > maxDepth) parts = parts.GetRange(parts.Count - maxDepth, maxDepth);
            
            parts.Reverse();
            
            return parts.ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }


    /// <summary>
    /// 异步行为实现 记录执行前后和异常时的日志
    /// </summary>
    public async ValueTask<T> InvokeAsync<T>(InvocationContext ctx, Func<ValueTask<T>> next)
    {
        var logger = ctx.Logger;
        var logInfo = ctx.Log && logger?.IsEnabled(LogLevel.Information) == true;
        var logError = logger?.IsEnabled(LogLevel.Error) == true;

        if (!logInfo)
        {
            try
            {
                return await next();
            }
            catch (Exception ex)
            {
                var callerOnly = BuildCallerChainArray();
                
                var exPayload = new Dictionary<string, object?>
                {
                    ["event"] = "exception",
                    ["method"] = ctx.Method,
                    ["exception"] = new Dictionary<string, object?>
                    {
                        ["source"] = ex.Source,
                        ["message"] = ex.Message,
                        ["stackTrace"] = ex.StackTrace,
                        ["innerSource"] = ex.InnerException?.Source,
                        ["innerMessage"] = ex.InnerException?.Message,
                        ["innerStackTrace"] = ex.InnerException?.StackTrace,
                    }
                };
                
                if (ctx.Args is not null) exPayload["args"] = ctx.Args;
                
                if (callerOnly.Length > 0) exPayload["caller"] = callerOnly;
                
                logger?.LogError(JsonUtil.ToJson(exPayload));
                
                throw;
            }
        }

        Stopwatch sw = Stopwatch.StartNew();
        
        var callerChain = BuildCallerChainArray();
        var hasArgs = ctx.Args is not null;

        var payload = new Dictionary<string, object?>
        {
            ["event"] = "executing",
            ["method"] = ctx.Method,
        };
        
        payload["traceId"] = ctx.TraceId;
        
        if (hasArgs) payload["args"] = ctx.Args;
        
        if (callerChain.Length > 0) payload["caller"] = callerChain;
        
        logger?.LogInformation(JsonUtil.ToJson(payload));

        try
        {
            var result = await next();
            sw.Stop();

            var payload2 = new Dictionary<string, object?>
            {
                ["event"] = "executed",
                ["method"] = ctx.Method,
                ["durationMs"] = sw.ElapsedMilliseconds,
            };
            
            payload2["traceId"] = ctx.TraceId;
            
            if (callerChain.Length > 0) payload2["caller"] = callerChain;
            
            if (ctx.HasReturnValue && ctx.AllowReturnSerialization)
            {
                payload2["result"] = result;
            }
            
            if (hasArgs) payload2["args"] = ctx.Args;
            
            logger?.LogInformation(JsonUtil.ToJson(payload2));

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            
            if (logError)
            {
                var exPayload = new Dictionary<string, object?>
                {
                    ["event"] = "exception",
                    ["method"] = ctx.Method,
                    ["exception"] = new Dictionary<string, object?>
                    {
                        ["source"] = ex.Source,
                        ["message"] = ex.Message,
                        ["stackTrace"] = ex.StackTrace,
                        ["innerSource"] = ex.InnerException?.Source,
                        ["innerMessage"] = ex.InnerException?.Message,
                        ["innerStackTrace"] = ex.InnerException?.StackTrace,
                    }
                };
                
                exPayload["traceId"] = ctx.TraceId;
                
                if (ctx.Args is not null) exPayload["args"] = ctx.Args;
                
                if (callerChain.Length > 0) exPayload["caller"] = callerChain;
                
                exPayload["durationMs"] = sw.ElapsedMilliseconds;
                
                logger?.LogError(JsonUtil.ToJson(exPayload));
            }
            throw;
        }
    }


    /// <summary>
    /// 同步行为在方法执行前的钩子 负责记录开始时间和必要日志
    /// </summary>
    public void OnBefore(InvocationContext ctx)
    {
        var logger = ctx.Logger;
        
        ctx.SetFeature(new LoggingState { StartTicks = Stopwatch.GetTimestamp() });

        if (ctx.Log && logger?.IsEnabled(LogLevel.Information) == true)
        {
            var callerChain = BuildCallerChainArray();
            
            var payload = new Dictionary<string, object?>
            {
                ["event"] = "executing",
                ["method"] = ctx.Method,
            };
            
            payload["traceId"] = ctx.TraceId;
            
            if (ctx.Args is not null) payload["args"] = ctx.Args;
            
            if (callerChain.Length > 0) payload["caller"] = callerChain;
            
            logger?.LogInformation(JsonUtil.ToJson(payload));
        }
    }


    /// <summary>
    /// 同步行为在方法成功执行后的钩子 负责记录耗时和返回结果
    /// </summary>
    public void OnAfter(InvocationContext ctx, object? result)
    {
        var logger = ctx.Logger;
        var st = ctx.GetFeature<LoggingState>();

        if (ctx.Log && logger?.IsEnabled(LogLevel.Information) == true)
        {
            var payload = new Dictionary<string, object?>
            {
                ["event"] = "executed",
                ["method"] = ctx.Method,
            };
            
            payload["traceId"] = ctx.TraceId;
            
            if (ctx.HasReturnValue && ctx.AllowReturnSerialization)
            {
                payload["result"] = result;
            }
            
            if (ctx.Args is not null) payload["args"] = ctx.Args;
            
            if (st is not null)
            {
                var elapsedMs = (Stopwatch.GetTimestamp() - st.StartTicks) * 1000.0 / Stopwatch.Frequency;
                payload["durationMs"] = (long)elapsedMs;
            }
            
            logger?.LogInformation(JsonUtil.ToJson(payload));
        }
    }


    /// <summary>
    /// 同步行为在方法或后续行为抛出异常时的钩子 负责记录异常日志
    /// </summary>
    public void OnException(InvocationContext ctx, Exception ex)
    {
        var logger = ctx.Logger;
        
        var st = ctx.GetFeature<LoggingState>();

        if (logger?.IsEnabled(LogLevel.Error) == true)
        {
            var callerOnly = BuildCallerChainArray();
            
            var exPayload = new Dictionary<string, object?>
            {
                ["event"] = "exception",
                ["method"] = ctx.Method,
                ["exception"] = new Dictionary<string, object?>
                {
                    ["source"] = ex.Source,
                    ["message"] = ex.Message,
                    ["stackTrace"] = ex.StackTrace,
                    ["innerSource"] = ex.InnerException?.Source,
                    ["innerMessage"] = ex.InnerException?.Message,
                    ["innerStackTrace"] = ex.InnerException?.StackTrace,
                }
            };
            
            exPayload["traceId"] = ctx.TraceId;
            
            if (ctx.Args is not null) exPayload["args"] = ctx.Args;
            
            if (callerOnly.Length > 0) exPayload["caller"] = callerOnly;
            
            if (st is not null)
            {
                var elapsedMs = (Stopwatch.GetTimestamp() - st.StartTicks) * 1000.0 / Stopwatch.Frequency;
                exPayload["durationMs"] = (long)elapsedMs;
            }
            
            logger?.LogError(JsonUtil.ToJson(exPayload));
        }
    }


}

