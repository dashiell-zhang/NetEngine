using Common;
using Microsoft.Extensions.Logging;
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


    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {

        if (IsEnabled(logLevel))
        {

            if (state != null && state.ToString() != null)
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


                    var log = new
                    {
                        Category = categoryName,
                        Level = logLevel.ToString(),
                        Content = logContent
                    };

                    string logStr = JsonHelper.ObjectToJson(log);
                    logWriter.Enqueue(logStr);

                }
            }

        }
    }


    /// <summary>
    /// 收集当前所有 Scope 并序列化为 JSON
    /// </summary>
    private string? CaptureScopes()
    {
        if (scopeProvider is null)
        {
            return null;
        }

        StringBuilder builder = new();

        scopeProvider.ForEachScope((scope, state) =>
        {
            if (scope is null)
            {
                return;
            }

            if (state.Length > 0)
            {
                state.Append(' ');
            }

            try
            {
                state.Append(JsonHelper.ObjectToJson(scope));
            }
            catch
            {
                state.Append(scope);
            }
        }, builder);

        return builder.Length == 0 ? null : builder.ToString();
    }

}
