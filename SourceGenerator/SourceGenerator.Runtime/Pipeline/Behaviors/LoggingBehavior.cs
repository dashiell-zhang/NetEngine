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
    /// 异步行为实现 记录执行前后和异常时的日志
    /// </summary>
    public async ValueTask<T> InvokeAsync<T>(InvocationContext ctx, Func<ValueTask<T>> next)
    {
        var logger = ctx.Logger;
        var logInfo = logger?.IsEnabled(LogLevel.Information) == true;
        var logError = logger?.IsEnabled(LogLevel.Error) == true;

        if (!logInfo && !logError)
        {
            return await next();
        }

        if (!logInfo)
        {
            try
            {
                return await next();
            }
            catch (Exception ex)
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
                
                if (ctx.Args is not null) exPayload["args"] = ctx.Args;
                
                logger?.LogError(JsonUtil.ToJson(exPayload));
                
                throw;
            }
        }

        Stopwatch sw = Stopwatch.StartNew();
        var hasArgs = ctx.Args is not null;

        var payload = new Dictionary<string, object?>
        {
            ["event"] = "executing",
            ["method"] = ctx.Method,
        };
        
        payload["traceId"] = ctx.TraceId;
        
        if (hasArgs) payload["args"] = ctx.Args;
        
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
        var logInfo = logger?.IsEnabled(LogLevel.Information) == true;
        var logError = logger?.IsEnabled(LogLevel.Error) == true;

        if (!logInfo && !logError)
        {
            return;
        }

        ctx.SetFeature(new LoggingState { StartTicks = Stopwatch.GetTimestamp() });

        if (logInfo)
        {
            var payload = new Dictionary<string, object?>
            {
                ["event"] = "executing",
                ["method"] = ctx.Method,
            };
            
            payload["traceId"] = ctx.TraceId;
            
            if (ctx.Args is not null) payload["args"] = ctx.Args;
            
            logger?.LogInformation(JsonUtil.ToJson(payload));
        }
    }


    /// <summary>
    /// 同步行为在方法成功执行后的钩子 负责记录耗时和返回结果
    /// </summary>
    public void OnAfter(InvocationContext ctx, object? result)
    {
        var logger = ctx.Logger;

        if (logger?.IsEnabled(LogLevel.Information) == true)
        {
            var st = ctx.GetFeature<LoggingState>();
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

        if (logger?.IsEnabled(LogLevel.Error) == true)
        {
            var st = ctx.GetFeature<LoggingState>();
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
            
            if (st is not null)
            {
                var elapsedMs = (Stopwatch.GetTimestamp() - st.StartTicks) * 1000.0 / Stopwatch.Frequency;
                exPayload["durationMs"] = (long)elapsedMs;
            }
            
            logger?.LogError(JsonUtil.ToJson(exPayload));
        }
    }


}
