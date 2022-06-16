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

        private readonly LoggerSetting loggerSetting;

        public LocalFileLogger(string categoryName, LoggerSetting loggerConfiguration)
        {
            this.categoryName = categoryName;

            string basePath = Directory.GetCurrentDirectory().Replace("\\", "/") + "/Log/";

            if (Directory.Exists(basePath) == false)
            {
                Directory.CreateDirectory(basePath);
            }

            logPath = basePath + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".log";

            this.loggerSetting = loggerConfiguration;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return default!;
        }


        public bool IsEnabled(LogLevel logLevel)
        {
            if (logLevel != LogLevel.None && logLevel >= loggerSetting.MinLogLevel)
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
                                AppSign = loggerSetting.AppSign,
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
