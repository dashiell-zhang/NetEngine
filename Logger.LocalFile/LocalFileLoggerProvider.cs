using Logger.LocalFile.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;

namespace Logger.LocalFile
{
    public class LocalFileLoggerProvider : ILoggerProvider
    {

        private readonly LoggerSetting loggerSetting;


        private readonly ConcurrentDictionary<string, LocalFileLogger> loggers = new();


        public LocalFileLoggerProvider(IOptionsMonitor<LoggerSetting> config)
        {
            loggerSetting = config.CurrentValue;
        }



        public ILogger CreateLogger(string categoryName)
        {
            return loggers.GetOrAdd(categoryName, new LocalFileLogger(categoryName, loggerSetting));
        }

        public void Dispose()
        {
            loggers.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
