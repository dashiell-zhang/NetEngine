using Common;
using Logger.DataBase.Models;
using Microsoft.Extensions.Logging;
using Repository.Database;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Logger.DataBase;

/// <summary>
/// 数据库日志记录器
/// </summary>
public class DataBaseLogger(string categoryName, LoggerSetting loggerSetting, DataBaseLogWriter logWriter, IExternalScopeProvider? scopeProvider) : ILogger
{

    /// <summary>
    /// 缓存记录器创建时的机器名，避免每条日志重复获取
    /// </summary>
    private readonly string machineName = Environment.MachineName;


    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return scopeProvider?.Push(state);
    }


    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None;
    }


    /// <summary>
    /// 捕获调用现场的日志内容并尝试立即入队
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

                    if (categoryName.StartsWith("Microsoft.EntityFrameworkCore"))
                    {
                        var stackTrace = new StackTrace(true);
                        var frames = stackTrace.GetFrames();

                        var relevantFrames = frames?.Where(t => t.GetFileName() != null).ToList();

                        if (relevantFrames?.Count > 1)
                        {
                            var relevantFrame = relevantFrames[1];

                            if (relevantFrame != null)
                            {
                                var logMsg = new
                                {
                                    message = logContent,
                                    stackFrame = new
                                    {
                                        fullName = relevantFrame.GetMethod()?.DeclaringType?.FullName,
                                        methodName = relevantFrame.GetMethod()?.Name,
                                        src = $"{relevantFrame.GetFileName()}:line {relevantFrame.GetFileLineNumber()}"
                                    }
                                };

                                logContent = JsonHelper.ObjectToJson(logMsg);
                            }
                        }
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

                    Log log = new()
                    {
                        CreateTime = DateTimeOffset.UtcNow,
                        Project = loggerSetting.Project,
                        MachineName = machineName,
                        Category = categoryName,
                        Level = GetLevelName(logLevel),
                        Content = logContent
                    };


                    logWriter.Enqueue(log, logLevel);
                }
            }

        }

    }


    /// <summary>
    /// 使用固定文本表示标准日志级别，未定义值保留枚举字符串转换
    /// </summary>
    private static string GetLevelName(LogLevel logLevel)
    {

        return logLevel switch
        {
            LogLevel.Trace => nameof(LogLevel.Trace),
            LogLevel.Debug => nameof(LogLevel.Debug),
            LogLevel.Information => nameof(LogLevel.Information),
            LogLevel.Warning => nameof(LogLevel.Warning),
            LogLevel.Error => nameof(LogLevel.Error),
            LogLevel.Critical => nameof(LogLevel.Critical),
            LogLevel.None => nameof(LogLevel.None),
            _ => logLevel.ToString()
        };

    }


    /// <summary>
    /// 收集当前所有 Scope 并序列化为 JSON，仅在存在有效 Scope 时创建文本缓冲
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
