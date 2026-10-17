using Common;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text;

namespace Logger.LocalFile;

/// <summary>
/// 本地文件日志记录器
/// </summary>
public class LocalFileLogger(string categoryName, LocalFileLogWriter logWriter, IExternalScopeProvider? scopeProvider) : ILogger
{

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return scopeProvider?.Push(state);
    }


    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None;
    }


    /// <summary>
    /// 捕获消息、异常和 Scope 快照并立即尝试入队，完整日志在后台序列化
    /// </summary>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {

        if (IsEnabled(logLevel))
        {

            if (state != null)
            {
                var logContent = state.ToString();

                if (logContent != null)
                {
                    var scopeText = CaptureScopes();

                    if (!string.IsNullOrEmpty(scopeText))
                    {
                        logContent = scopeText + " " + logContent;
                    }

                    if (exception != null)
                    {
                        var logMsg = new
                        {
                            message = logContent,
                            error = new
                            {
                                exception?.Source,
                                exception?.Message,
                                exception?.StackTrace
                            }
                        };

                        logContent = JsonHelper.ObjectToJson(logMsg);
                    }


                    logWriter.Enqueue(categoryName, logContent, logLevel);

                }
            }

        }

    }


    /// <summary>
    /// 收集当前所有 Scope 并序列化为 JSON，仅在存在有效 Scope 时分配文本缓冲
    /// </summary>
    private string? CaptureScopes()
    {

        if (scopeProvider is null)
        {
            return null;
        }

        StrongBox<StringBuilder?> buffer = new();

        scopeProvider.ForEachScope(static (scope, state) =>
        {
            if (scope is null)
            {
                return;
            }

            var builder = state.Value ??= new StringBuilder();
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            try
            {
                builder.Append(JsonHelper.ObjectToJson(scope));
            }
            catch
            {
                builder.Append(scope);
            }
        }, buffer);

        return buffer.Value is { Length: > 0 } text ? text.ToString() : null;

    }

}
