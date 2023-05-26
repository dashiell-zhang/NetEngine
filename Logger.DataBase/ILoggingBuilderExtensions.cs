using Logger.DataBase.Models;
using Logger.DataBase.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Logger.DataBase
{

    public static class ILoggingBuilderExtensions
    {

        public static void AddDataBaseLogger(this ILoggingBuilder builder, Action<LoggerSetting> action)
        {
            builder.Services.Configure(action);
            builder.Services.AddSingleton<ILoggerProvider, DataBaseLoggerProvider>();
            builder.Services.AddHostedService<LogClearTask>();
            builder.Services.AddHostedService<LogSaveTask>();
        }
    }
}