using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace SourceGenerator.Runtime;


public sealed class LoggingBehavior : IInvocationBehavior
{
    private static string[] BuildCallerChainArray(int maxDepth = 100)
    {
        try
        {
            var st = new StackTrace(skipFrames: 1, fNeedFileInfo: false);
            var frames = st.GetFrames();
            if (frames is null || frames.Length == 0) return Array.Empty<string>();

            var parts = new List<string>(7);
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
                parts.Add(name);
            }
            if (parts.Count == 0) return Array.Empty<string>();
            if (parts.Count > maxDepth)
            {
                parts = parts.GetRange(parts.Count - maxDepth, maxDepth);
            }
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
                        ["Source"] = ex.Source,
                        ["Message"] = ex.Message,
                        ["StackTrace"] = ex.StackTrace,
                        ["InnerSource"] = ex.InnerException?.Source,
                        ["InnerMessage"] = ex.InnerException?.Message,
                        ["InnerStackTrace"] = ex.InnerException?.StackTrace,
                    }
                };
                // include args if present
                if (!string.IsNullOrEmpty(ctx.ArgsJson)) exPayload["args"] = ctx.ArgsJson;
                if (callerOnly.Length > 0) exPayload["caller"] = callerOnly;
                logger?.LogError(JsonUtil.ToLogJson(exPayload));

                throw;
            }
        }

        Stopwatch? sw = Stopwatch.StartNew();

        var callerChain = BuildCallerChainArray();
        var hasArgs = !string.IsNullOrEmpty(ctx.ArgsJson);
        var payload = new Dictionary<string, object?>
        {
            ["event"] = "executing",
            ["method"] = ctx.Method,
        };
        payload["traceId"] = ctx.TraceId;
        if (hasArgs) payload["args"] = ctx.ArgsJson;
        if (callerChain.Length > 0) payload["caller"] = callerChain;
        logger?.LogInformation(JsonUtil.ToLogJson(payload));

        try
        {
            var result = await next();

            if (sw is not null)
            {
                sw.Stop();
                var payload2 = new Dictionary<string, object?>
                {
                    ["event"] = "executed",
                    ["method"] = ctx.Method,
                    ["duration_ms"] = sw.ElapsedMilliseconds,
                };
                payload2["traceId"] = ctx.TraceId;
                if (callerChain.Length > 0) payload2["caller"] = callerChain;
                logger?.LogInformation(JsonUtil.ToLogJson(payload2));
            }

            if (ctx.HasReturnValue)
            {
                var payload3 = new Dictionary<string, object?>
                {
                    ["event"] = "return",
                    ["method"] = ctx.Method,
                    ["result"] = result,
                };
                payload3["traceId"] = ctx.TraceId;
                if (callerChain.Length > 0) payload3["caller"] = callerChain;
                logger?.LogInformation(JsonUtil.ToLogJson(payload3));
            }

            return result;
        }
        catch (Exception ex)
        {
            if (sw is not null) sw.Stop();
            if (logError)
            {
                var exPayload = new Dictionary<string, object?>
                {
                    ["event"] = "exception",
                    ["method"] = ctx.Method,
                    ["exception"] = new Dictionary<string, object?>
                    {
                        ["Source"] = ex.Source,
                        ["Message"] = ex.Message,
                        ["StackTrace"] = ex.StackTrace,
                        ["InnerSource"] = ex.InnerException?.Source,
                        ["InnerMessage"] = ex.InnerException?.Message,
                        ["InnerStackTrace"] = ex.InnerException?.StackTrace,
                    }
                };
                exPayload["traceId"] = ctx.TraceId;
                if (!string.IsNullOrEmpty(ctx.ArgsJson)) exPayload["args"] = ctx.ArgsJson;
                if (callerChain.Length > 0) exPayload["caller"] = callerChain;
                if (sw is not null) exPayload["duration_ms"] = sw.ElapsedMilliseconds;
                logger?.LogError(JsonUtil.ToLogJson(exPayload));
            }
            throw;
        }
    }
}
