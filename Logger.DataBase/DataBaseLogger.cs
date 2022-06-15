using Common;
using Logger.DataBase.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Repository.Database;
using System;

namespace Logger.DataBase
{
    public class DataBaseLogger : ILogger
    {

        private readonly string categoryName;

        private readonly PooledDbContextFactory<DatabaseContext> dbContextFactory;

        private readonly SnowflakeHelper snowflakeHelper;

        private readonly LoggerConfiguration loggerConfiguration;


        public DataBaseLogger(string categoryName, LoggerConfiguration loggerConfiguration, SnowflakeHelper snowflakeHelper)
        {
            this.categoryName = categoryName;

            this.loggerConfiguration = loggerConfiguration;
            var options = new DbContextOptionsBuilder<DatabaseContext>().UseSqlServer(loggerConfiguration.DataBaseConnection).Options;

            dbContextFactory = new PooledDbContextFactory<DatabaseContext>(options, 100);

            this.snowflakeHelper = snowflakeHelper;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return default!;
        }


        public bool IsEnabled(LogLevel logLevel)
        {
            if (logLevel != LogLevel.None && logLevel >= loggerConfiguration.MinLogLevel)
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

                            using (var db = dbContextFactory.CreateDbContext())
                            {
                                TLog log = new();
                                log.Id = snowflakeHelper.GetId();
                                log.CreateTime = DateTime.UtcNow;
                                log.AppSign = loggerConfiguration.AppSign;
                                log.Category = categoryName;
                                log.Level = logLevel.ToString();
                                log.Content = logContent;

                                db.TLog.Add(log);
                                db.SaveChanges();
                            }
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
