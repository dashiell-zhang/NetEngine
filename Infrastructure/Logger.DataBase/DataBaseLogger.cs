using Common;
using IdentifierGenerator;
using Logger.DataBase.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Repository.Database;
using System.Diagnostics;
using System.Text;

namespace Logger.DataBase
{
    public class DataBaseLogger(string categoryName, LoggerSetting loggerSetting, IServiceProvider serviceProvider) : ILogger
    {
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

                        var idService = serviceProvider.GetRequiredService<IdService>();

                        TLog log = new()
                        {
                            Id = idService.GetId(),
                            CreateTime = DateTimeOffset.UtcNow,
                            Project = loggerSetting.Project,
                            MachineName = Environment.MachineName,
                            Category = categoryName,
                            Level = logLevel.ToString(),
                            Content = logContent
                        };


                        string logStr = JsonHelper.ObjectToJson(log);


                        string basePath = Path.Combine(Directory.GetCurrentDirectory(), "Logs");

                        if (Directory.Exists(basePath) == false)
                        {
                            Directory.CreateDirectory(basePath);
                        }

                        var logPath = Path.Combine(basePath, log.Id + ".log");
                        File.WriteAllTextAsync(logPath, logStr, Encoding.UTF8);
                    }
                }

            }
        }
    }
}
