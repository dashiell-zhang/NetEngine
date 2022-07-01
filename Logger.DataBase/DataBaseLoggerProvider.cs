using Common;
using Logger.DataBase.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Logger.DataBase
{
    public class DataBaseLoggerProvider : ILoggerProvider
    {

        private readonly LoggerSetting loggerConfiguration;

        private readonly SnowflakeHelper snowflakeHelper;

        private readonly ConcurrentDictionary<string, DataBaseLogger> loggers = new();

        private readonly string ip = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList.FirstOrDefault(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString()!;



        public DataBaseLoggerProvider(IOptionsMonitor<LoggerSetting> config, SnowflakeHelper snowflakeHelper)
        {
            loggerConfiguration = config.CurrentValue;
            this.snowflakeHelper = snowflakeHelper;
        }



        public ILogger CreateLogger(string categoryName)
        {
            return loggers.GetOrAdd(categoryName, new DataBaseLogger(categoryName, loggerConfiguration, snowflakeHelper, ip));
        }

        public void Dispose()
        {
            loggers.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
