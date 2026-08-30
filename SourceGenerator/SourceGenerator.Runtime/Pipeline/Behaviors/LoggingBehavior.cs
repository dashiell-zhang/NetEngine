using Microsoft.Extensions.Logging;
using SourceGenerator.Runtime.Serialization;
using System.Diagnostics;

namespace SourceGenerator.Runtime.Pipeline.Behaviors;

/// <summary>
/// 在调用前后和异常时记录结构化日志 支持异步和同步行为接口
/// </summary>
public sealed class LoggingBehavior : IInvocationAsyncBehavior, IInvocationBehavior, IAsyncStreamResultCaptureBehavior
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

        var startTimestamp = Stopwatch.GetTimestamp();

        if (logInfo)
        {
            LogExecuting(ctx);
        }

        try
        {
            var result = await next();

            if (logInfo)
            {
                LogExecuted(ctx, result, GetElapsedMilliseconds(startTimestamp));
            }

            return result;
        }
        catch (Exception ex)
        {
            if (logError)
            {
                LogException(ctx, ex, GetElapsedMilliseconds(startTimestamp));
            }

            throw;
        }

    }


    /// <summary>
    /// 判断当前调用是否需要为结果日志捕获异步流元素
    /// </summary>
    /// <param name="context">当前调用上下文</param>
    /// <returns>Information 日志启用且允许记录返回值时返回 true</returns>
    public bool ShouldCaptureAsyncStreamItems(InvocationContext context)
        => context.HasReturnValue
           && context.AllowReturnSerialization
           && context.Logger?.IsEnabled(LogLevel.Information) == true;


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
            LogExecuting(ctx);
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
            long? durationMs = st is null ? null : GetElapsedMilliseconds(st.StartTicks);
            LogExecuted(ctx, result, durationMs);
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
            long? durationMs = st is null ? null : GetElapsedMilliseconds(st.StartTicks);
            LogException(ctx, ex, durationMs);
        }

    }


    /// <summary>
    /// 记录方法开始执行日志
    /// </summary>
    /// <param name="ctx">当前调用上下文</param>
    private static void LogExecuting(InvocationContext ctx)
    {

        var payload = CreatePayload(ctx, "executing");
        ctx.Logger?.LogInformation(JsonUtil.ToJson(payload));

    }


    /// <summary>
    /// 记录方法成功执行日志
    /// </summary>
    /// <param name="ctx">当前调用上下文</param>
    /// <param name="result">方法返回结果</param>
    /// <param name="durationMs">执行耗时毫秒数</param>
    private static void LogExecuted(InvocationContext ctx, object? result, long? durationMs)
    {

        var payload = CreatePayload(ctx, "executed");

        if (ctx.HasReturnValue && ctx.AllowReturnSerialization)
        {
            payload["result"] = result;
        }

        if (durationMs is not null)
        {
            payload["durationMs"] = durationMs.Value;
        }

        ctx.Logger?.LogInformation(JsonUtil.ToJson(payload));

    }


    /// <summary>
    /// 记录方法执行异常日志
    /// </summary>
    /// <param name="ctx">当前调用上下文</param>
    /// <param name="exception">方法执行异常</param>
    /// <param name="durationMs">执行耗时毫秒数</param>
    private static void LogException(InvocationContext ctx, Exception exception, long? durationMs)
    {

        var payload = CreatePayload(ctx, "exception");
        payload["exception"] = new Dictionary<string, object?>
        {
            ["source"] = exception.Source,
            ["message"] = exception.Message,
            ["stackTrace"] = exception.StackTrace,
            ["innerSource"] = exception.InnerException?.Source,
            ["innerMessage"] = exception.InnerException?.Message,
            ["innerStackTrace"] = exception.InnerException?.StackTrace,
        };

        if (durationMs is not null)
        {
            payload["durationMs"] = durationMs.Value;
        }

        ctx.Logger?.LogError(JsonUtil.ToJson(payload));

    }


    /// <summary>
    /// 创建包含公共调用信息的日志载荷
    /// </summary>
    /// <param name="ctx">当前调用上下文</param>
    /// <param name="eventName">日志事件名称</param>
    /// <returns>包含公共字段的日志载荷</returns>
    private static Dictionary<string, object?> CreatePayload(InvocationContext ctx, string eventName)
    {

        var payload = new Dictionary<string, object?>
        {
            ["event"] = eventName,
            ["method"] = ctx.Method,
            ["traceId"] = ctx.TraceId,
        };

        if (ctx.Args is not null)
        {
            payload["args"] = ctx.Args;
        }

        return payload;

    }


    /// <summary>
    /// 根据起始时间戳计算已执行的毫秒数
    /// </summary>
    /// <param name="startTimestamp">起始时间戳</param>
    /// <returns>已执行的毫秒数</returns>
    private static long GetElapsedMilliseconds(long startTimestamp)
    {

        return (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

    }


}
