using Common;
using IdentifierGenerator;
using Logger.DataBase.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Logger.DataBase
{
    public class DataBaseLoggerProvider(IOptionsMonitor<LoggerSetting> config, IdService idService) : ILoggerProvider
    {

        private readonly LoggerSetting loggerConfiguration = config.CurrentValue;
        private readonly ConcurrentDictionary<string, DataBaseLogger> loggers = new();

        public ILogger CreateLogger(string categoryName)
        {
            return loggers.GetOrAdd(categoryName, new DataBaseLogger(categoryName, loggerConfiguration, idService));
        }

        public void Dispose()
        {
            loggers.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
