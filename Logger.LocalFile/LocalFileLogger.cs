using Common;
using Microsoft.Extensions.Logging;
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

            basePath = Path.Combine(Directory.GetCurrentDirectory(), "Logs");

            if (Directory.Exists(basePath) == false)
            {
                Directory.CreateDirectory(basePath);
            }
        }



        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return default;
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
                            Category = categoryName,
                            Level = logLevel.ToString(),
                            Content = logContent
                        };

                        string logStr = JsonHelper.ObjectToJson(log);

                        var logPath = Path.Combine(basePath, DateTime.UtcNow.ToString("yyyyMMddHH") + ".log");

                        File.AppendAllText(logPath, logStr + Environment.NewLine, Encoding.UTF8);


                    }
                }

            }
        }
    }
}
