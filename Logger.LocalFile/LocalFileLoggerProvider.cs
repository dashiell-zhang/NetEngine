using Logger.LocalFile.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Logger.LocalFile
{
    public class LocalFileLoggerProvider : ILoggerProvider
    {

        private readonly LoggerConfiguration loggerConfiguration;


        private readonly ConcurrentDictionary<string, LocalFileLogger> loggers = new ();


        public LocalFileLoggerProvider(IOptionsMonitor<LoggerConfiguration> config)
        {
            loggerConfiguration = config.CurrentValue;
        }



        public ILogger CreateLogger(string categoryName)
        {
            return loggers.GetOrAdd(categoryName, new LocalFileLogger(categoryName, loggerConfiguration));
        }

        public void Dispose()
        {
            loggers.Clear();
        }
    }
}
