using Common;
using Logger.LocalFile.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Logger.LocalFile
{
    public class LocalFileLoggerProvider : ILoggerProvider
    {

        private readonly LoggerConfiguration loggerConfiguration;

        private readonly SnowflakeHelper snowflakeHelper;

        private readonly ConcurrentDictionary<string, LocalFileLogger> loggers = new ();


        public LocalFileLoggerProvider(IOptionsMonitor<LoggerConfiguration> config, SnowflakeHelper snowflakeHelper)
        {
            loggerConfiguration = config.CurrentValue;
            this.snowflakeHelper = snowflakeHelper;
        }



        public ILogger CreateLogger(string categoryName)
        {
            return loggers.GetOrAdd(categoryName, new LocalFileLogger(categoryName, loggerConfiguration, snowflakeHelper));
        }

        public void Dispose()
        {
            loggers.Clear();
        }
    }
}
