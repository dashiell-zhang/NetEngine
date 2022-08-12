using Common;
using Logger.DataBase.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Logger.DataBase
{
    public class DataBaseLoggerProvider : ILoggerProvider
    {

        private readonly LoggerSetting loggerConfiguration;

        private readonly IDHelper idHelper;

        private readonly ConcurrentDictionary<string, DataBaseLogger> loggers = new();



        public DataBaseLoggerProvider(IOptionsMonitor<LoggerSetting> config, IDHelper idHelper)
        {
            loggerConfiguration = config.CurrentValue;
            this.idHelper = idHelper;
        }



        public ILogger CreateLogger(string categoryName)
        {
            return loggers.GetOrAdd(categoryName, new DataBaseLogger(categoryName, loggerConfiguration, idHelper));
        }

        public void Dispose()
        {
            loggers.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
