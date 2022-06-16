using Common;
using Logger.LocalFile.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;

namespace Logger.LocalFile
{
    public class LocalFileLogger : ILogger
    {

        private readonly string categoryName;

        private readonly string logPath;

        private readonly LoggerConfiguration loggerConfiguration;

        public LocalFileLogger(string categoryName, LoggerConfiguration loggerConfiguration, SnowflakeHelper snowflakeHelper)
        {
            this.categoryName = categoryName;

            logPath = loggerConfiguration.LogFolderPath + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".log";

            this.loggerConfiguration = loggerConfiguration;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return default!;
        }


        public bool IsEnabled(LogLevel logLevel)
        {
            if (logLevel != LogLevel.None && logLevel >= loggerConfiguration.MinLogLevel)
            {
                return true;
            }
            else
            {
                return false;
            }
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

                        try
                        {
                            var log = new
                            {
                                CreateTime = DateTime.UtcNow,
                                AppSign = loggerConfiguration.AppSign,
                                Category = categoryName,
                                Level = logLevel.ToString(),
                                Content = logContent
                            };

                            string logStr = JsonHelper.ObjectToJson(log);

                            File.AppendAllText(logPath, logStr + Environment.NewLine, Encoding.UTF8);

                        }
                        catch
                        {

                        }
                    }
                }

            }
        }
    }
}
