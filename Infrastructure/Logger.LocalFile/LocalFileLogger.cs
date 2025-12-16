using Common;
using Microsoft.Extensions.Logging;

namespace Logger.LocalFile;

public class LocalFileLogger : ILogger
{

    private readonly string categoryName;

    private readonly LocalFileLogWriter logWriter;


    public LocalFileLogger(string categoryName, LocalFileLogWriter logWriter)
    {
        this.categoryName = categoryName;
        this.logWriter = logWriter;
    }


    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return default;
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

}
