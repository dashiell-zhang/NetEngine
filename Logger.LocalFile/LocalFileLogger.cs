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


        private readonly string basePath;

        public LocalFileLogger(string categoryName)
        {
            this.categoryName = categoryName;

            basePath = Directory.GetCurrentDirectory().Replace("\\", "/") + "/Logs/";

            if (Directory.Exists(basePath) == false)
            {
                Directory.CreateDirectory(basePath);
            }
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return default!;
        }


        public bool IsEnabled(LogLevel logLevel)
        {
            if (logLevel != LogLevel.None)
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


                        var log = new
                        {
                            CreateTime = DateTime.UtcNow,
                            Category = categoryName,
                            Level = logLevel.ToString(),
                            Content = logContent
                        };

                        string logStr = JsonHelper.ObjectToJson(log);

                        var logPath = basePath + DateTime.UtcNow.ToString("yyyyMMddHH") + ".log";

                        File.AppendAllText(logPath, logStr + Environment.NewLine, Encoding.UTF8);


                    }
                }

            }
        }
    }
}
