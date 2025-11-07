using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Security.Claims;

namespace SourceGenerator.Runtime.Pipeline.Behaviors;

public sealed class LoggingBehavior : IInvocationAsyncBehavior, IInvocationBehavior
{
    private sealed class LoggingState
    {
        public long StartTicks { get; set; }
    }

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
            if (ctx.HasReturnValue)
            {
                var r = (T?)result;
                if (r is not null && IsAsyncEnumerableType(r.GetType()))
                    payload2["result"] = "<async-stream>";
                else
                    payload2["result"] = Sanitize(r);
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

    public void OnBefore(InvocationContext ctx)
    {
        var logger = ctx.Logger;
        // start timing for sync three-phase path
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
            if (ctx.HasReturnValue)
            {
                if (result is not null && IsAsyncEnumerableType(result.GetType()))
                    payload["result"] = "<async-stream>";
                else
                    payload["result"] = Sanitize(result);
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


    private static bool IsAsyncEnumerableType(Type t)
    {
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
            return true;
        return t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));
    }

    private static object? Sanitize(object? value)
    {
        if (value is null) return null;
        if (value is string || value.GetType().IsPrimitive) return value;
        if (value is System.Threading.CancellationToken) return "<cancellation-token>";
        if (value is System.Threading.CancellationTokenSource) return "<cancellation-token-source>";
        if (value is Stream) return "<stream>";
        if (value is TextReader) return "<text-reader>";
        if (value is TextWriter) return "<text-writer>";
        var fn = value.GetType().FullName ?? string.Empty;
        if (fn.StartsWith("System.Threading.Channels.ChannelReader`", StringComparison.Ordinal)) return "<channel-reader>";
        if (fn.StartsWith("System.Threading.Channels.ChannelWriter`", StringComparison.Ordinal)) return "<channel-writer>";
        if (fn.StartsWith("System.IO.Pipelines.PipeReader", StringComparison.Ordinal)) return "<pipe-reader>";
        if (fn.StartsWith("System.IO.Pipelines.PipeWriter", StringComparison.Ordinal)) return "<pipe-writer>";
        if (value is ClaimsPrincipal || value is System.Security.Principal.IPrincipal) return "<principal>";
        if (value is DbConnection || value is IDbConnection) return "<db-connection>";
        if (value is DbTransaction) return "<db-transaction>";
        if (value is DbCommand) return "<db-command>";
        if (value is HttpClient) return "<http-client>";
        if (value is HttpRequestMessage) return "<http-request>";
        if (value is HttpResponseMessage) return "<http-response>";
        if (value is Delegate) return "<delegate>";
        if (value is Expression) return "<expression>";
        if (value is byte[] bytes) return new Dictionary<string, object> { ["kind"] = "bytes", ["length"] = bytes.Length };
        if (value is ReadOnlyMemory<byte> rom) return new Dictionary<string, object> { ["kind"] = "bytes", ["length"] = rom.Length };
        if (value is Memory<byte> mem) return new Dictionary<string, object> { ["kind"] = "bytes", ["length"] = mem.Length };
        return value;
    }

}

