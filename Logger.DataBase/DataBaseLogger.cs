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

        private readonly LoggerSetting loggerSetting;


        public DataBaseLogger(string categoryName, LoggerSetting loggerSetting, SnowflakeHelper snowflakeHelper)
        {
            this.categoryName = categoryName;

            this.loggerSetting = loggerSetting;
            var options = new DbContextOptionsBuilder<DatabaseContext>().UseSqlServer(loggerSetting.DataBaseConnection).Options;

            dbContextFactory = new PooledDbContextFactory<DatabaseContext>(options, 100);

            this.snowflakeHelper = snowflakeHelper;
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

                        try
                        {

                            using var db = dbContextFactory.CreateDbContext();
                            TLog log = new()
                            {
                                Id = snowflakeHelper.GetId(),
                                CreateTime = DateTime.UtcNow,
                                AppSign = loggerSetting.AppSign,
                                Category = categoryName,
                                Level = logLevel.ToString(),
                                Content = logContent
                            };

                            db.TLog.Add(log);
                            db.SaveChanges();
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
